using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

// Control+K+C to comment all selected, Control+K+U to uncomment all selected.

// Flattening/Unflattening Arrays: https://stackoverflow.com/questions/21596373/compute-shaders-input-3d-array-of-floats
// Flatten: https://stackoverflow.com/questions/7367770/how-to-flatten-or-index-3d-array-in-1d-array
// Check out the above for which coordinate system is faster.
// 3d Volumetric: https://github.com/songshibo/UnityFluidDynamics/tree/main
// Jos Stam's Navier-Stokes https://www.dgp.toronto.edu/public_user/stam/reality/Research/pdf/GDC03.pdf
// 
// GPU: https://github.com/FlanOfFlans/Just-Smoke-No-Mirrors/tree/master
// Haven't checked yet: https://github.com/J-Ponzo/densityBasedFluidSimulation
//
// Render Options
// Ray marching, but says it's SPH unoptimized, https://github.com/AJTech2002/Smoothed-Particle-Hydrodynamics/tree/main
//
// Cellular Automata
// C++ Cellular Automaton with water pressure, claimed buggy: https://github.com/auneselva/CellularWater/blob/main/Source/UnrealCppManual/Private/WorldController.cpp
// Cellular Automaton physics: https://w-shadow.com/blog/2009/09/01/simple-fluid-simulation/
//
// Toady's article: http://web.archive.org/web/20161205035315/http://www.gamasutra.com/view/feature/3549/interview_the_making_of_dwarf_.php?page=9


[BurstCompile]
public class FluidSim : MonoBehaviour
{
    public int gridBoundsXZ = 96;
    public int gridBoundsY = 96;

    public float Viscosity = 0.5f; // How much of a cell's liquid moves under influence.
    public float Gravity = -9.8f;
    public int flowRate = 1;

    public bool RunSinglethreadSimulation = false;
    public bool RunMultithreadSimulation = false;
    public bool upDown = false;
    public bool leftRight = false;
    public bool forwardBack = false;

    [HideInInspector]
    public int maxDensity = 100;

    public bool randomizeOnStart = false;
    public bool randomseededOnStart = false;
    public bool iterativeOnStart = false;
    public bool alternateOnStart = false;
    public int alternateAmount = 2;
    public int alternateAmountCap = 100;

    NativeArray<int> cellGrid, newCellGrid;
    public int jobsPerThread = 64;
    public bool printDebugTime = false;

    public int randomSeed = 42;


    int IX(int x, int y, int z)
    {
        // [x+y*world.maxSize+z*world.maxSize*world.maxSizeY
        // x + (y * maxX) + (z * maxX * maxY);

        return x + y * gridBoundsXZ + z * gridBoundsXZ * gridBoundsY;
        // If I remember correctly, the Clamp was very expensive.
        //return Mathf.Clamp(x + y * gridBoundsSingle + z * gridBoundsSingle * gridBoundsSingle, 0, gridBoundsSingle * gridBoundsSingle * gridBoundsSingle);
    }


    /*
    public int to1D(int x, int y, int z)
    {
        return (z * xMax * yMax) + (y * xMax) + x;
    }

    public int[] to3D(int idx)
    {
        final int z = idx / (xMax * yMax);
        idx -= (z * xMax * yMax);
        final int y = idx / xMax;
        final int x = idx % xMax;
        return new int[] { x, y, z };

        */

        public int3 to3D(int idx)
    {
        int z = idx / (gridBoundsXZ * gridBoundsY);
        idx -= (z * gridBoundsXZ * gridBoundsY);
        int y = idx / gridBoundsXZ;
        int x = idx % gridBoundsXZ;
        return new int3(x, y, z);
    }

    private void OnDestroy()
    {
        newCellGrid.Dispose();
        cellGrid.Dispose();
    }

    // Start is called before the first frame update
    void Awake()
    {
        Debug.Log("TODO: Move .complete and such into LateUpdate or even next frame.");
        Debug.Log("TODO: Look into turning this into interlinking chunks.");
        Debug.Log("TODO: Figure out how to manage size better, maybe look at voxel datastructures, as they can have way more points.");
        Debug.Log("TODO: Figure out a way to handle more data than a single int. Can swap it for int4 for four bits of data(density, downPressure,flowDirection((use1-8 for directions, 9/0 up/down)),?), but that's still very limiting.");
        // Solution for above? https://forum.unity.com/threads/ijobparallelfor-with-nativearray-of-custom-structs-within.938669/
        // Also for above, references voxel data too. https://forum.unity.com/threads/make-class-a-struct-and-use-nativearrays-in-it.984246/
        // More job burst struct stuff https://forum.unity.com/threads/job-system-example-starting-with-simple-optimizations-using-a-nativearray-struct.540652/
        Debug.Log("TODO: Adjust the Jobs per Thread over time, seeking the fastest Millisecond. (Really should just move between 32-256");
        Debug.Log("Awakening fluid sim");

        // ================ v Array Definitions v ==================
        cellGrid = new NativeArray<int>(gridBoundsXZ * gridBoundsY * gridBoundsXZ, Allocator.Persistent);
        newCellGrid = new NativeArray<int>(gridBoundsXZ * gridBoundsY * gridBoundsXZ, Allocator.Persistent);
        // ==================================

        if (randomizeOnStart)
        {
            randomizeCells();
        }

        if(randomseededOnStart)
        {
            randomizeCellsSeeded();
        }

        if(iterativeOnStart)
        {
            iterateCells();
        }

        if(randomizeOnStart && iterativeOnStart)
        {
            randomizeAndIterateCells();
        }

        if(alternateOnStart)
        {
            alternateCells();
        }
    }

    
    [ContextMenu("Randomize")]
    [EButton.BeginHorizontal("Cell Level"), EButton]
    void randomizeCells()
    {
        randomSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(randomSeed);

        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {
            //cellGrid[i] = UnityEngine.Random.Range(0, 101);
            
            cellGrid[i] = UnityEngine.Random.Range(0, 21)*5;
        }
    }

    [EButton("RS")]
    void randomizeCellsSeeded()
    {
        Random.InitState(randomSeed);

        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {
            //cellGrid[i] = UnityEngine.Random.Range(0, 101);
            
            cellGrid[i] = UnityEngine.Random.Range(0, 21) * 5;
        }
    }

    [EButton]
    void iterateCells()
    {
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {

            cellGrid[i] = Mathf.Clamp(1 * (i % 32), 0, 100);
        }
    }

    [EButton]
    void randomizeAndIterateCells()
    {
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {

            cellGrid[i] = Mathf.Clamp(1 * (i % 32), 0, 100) * UnityEngine.Random.Range(1, 3);
        }
    }
    [EButton, EButton.EndHorizontal]
    void alternateCells()
    {
        int tracker = 0;
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {
            tracker--;
            if(tracker <= 0)
            {
                cellGrid[i] = alternateAmountCap;
                tracker = alternateAmount;
            }
            else
            {
                cellGrid[i] = 0;
            }
                
            //cellGrid[i] = Mathf.Clamp(i % alternateAmount * 100,0,alternateAmountCap);
        }
    }

    [EButton]
    public int SumOfDensity()
    {
        int totalDensity = 0;
        //int highestDensity = 0;
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {

            totalDensity += cellGrid[i];
            //if (cellGrid[i] > highestDensity)
              //  highestDensity = cellGrid[i];
        }

        //Debug.Log("Total Density: " + totalDensity + ", highest: " + highestDensity);
        return totalDensity;
    }



    [EButton]
    void MultithreadedMadness()
    {

        WaterLogicJob waterLogicJob = new WaterLogicJob()
        {
            originalCellGrid = cellGrid,
            gridBoundsXZ = this.gridBoundsXZ,
            gridBoundsY = this.gridBoundsY,
            Gravity = this.Gravity,
            maxDensity = this.maxDensity,
            FlowRate = flowRate,
            upDown = this.upDown,
            leftRight = this.leftRight,
            forwardBack = this.forwardBack,

            outputCellGrid = newCellGrid,
        };

        JobHandle waterLogicJobHandle = waterLogicJob.Schedule(cellGrid.Length, jobsPerThread);
        waterLogicJobHandle.Complete();

        //int readIdx = thirtythirtythirty;
        //Debug.Log(cellGrid[readIdx] + " : " + waterLogicJob.originalCellGrid[readIdx] + " : " +
        //    newCellGrid[readIdx] + " : " + waterLogicJob.outputCellGrid[readIdx]);

        newCellGrid = waterLogicJob.outputCellGrid;
        cellGrid.CopyFrom(newCellGrid);
    }

    //int thirtythirtythirty = 0;

    // Update is called once per frame
    void Update()
    {
        //float timePassed;
        //float time = Time.realtimeSinceStartup;
        Stopwatch stopwatch = Stopwatch.StartNew();
        stopwatch.Start();


        

        


        //if (RunSinglethreadSimulation)
            
        // int posIdx = IX(30, 30, 30);
        // thirtythirtythirty = posIdx;
        // int posIdxUp = IX(30, 31, 30);
        // int posIdxDown = IX(30, 29, 30);
        // 
        // Debug.Log(cellGrid[posIdxUp] + ":" + cellGrid[posIdx] + ":" + cellGrid[posIdxDown]);


        if (RunMultithreadSimulation)
        {
            MultithreadedMadness();
        }

        stopwatch.Stop();
        long elapsedTimeMilliseconds = stopwatch.ElapsedMilliseconds;
        long elapsedTicks = stopwatch.ElapsedTicks;
        //print($"Elapsed time in milliseconds: {elapsedTimeMilliseconds}");
        if(printDebugTime)
            print($"Elapsed ticks: {elapsedTicks}, 100,000 = ms");
        //timePassed = Time.realtimeSinceStartup - time;
        //Debug.Log("Time(ms): " + (timePassed*1000f));
    }

    public float GetCellValue(int x, int y, int z)
    {
        return cellGrid[IX(x, y, z)];
    }
}


