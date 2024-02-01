using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using Unity.Collections;

// Flattening/Unflattening Arrays: https://stackoverflow.com/questions/21596373/compute-shaders-input-3d-array-of-floats
// Check out the above for which coordinate system is faster.
// 3d Volumetric: https://github.com/songshibo/UnityFluidDynamics/tree/main
// 
// GPU: https://github.com/FlanOfFlans/Just-Smoke-No-Mirrors/tree/master
// Haven't checked yet: https://github.com/J-Ponzo/densityBasedFluidSimulation


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
    public bool iterativeOnStart = false;
    public bool alternateOnStart = false;
    public int alternateAmount = 2;
    public int alternateAmountCap = 100;

    NativeArray<int> cellGrid, newCellGrid;
    public int jobsPerThread = 64;
    public bool printDebugTime = false;


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
        Debug.Log("TODO: Figure out to3D math to squish the bounding box. 96x32x96 is ideal.");
        Debug.Log("TODO: Move .complete and such into LateUpdate or even next frame.");
        Debug.Log("TODO: Look into turning this into interlinking chunks.");
        Debug.Log("TODO: Figure out how to manage size better, maybe look at voxel datastructures, as they can have way more points.");
        Debug.Log("Awakening fluid sim");

        // ================ v Array Definitions v ==================
        Debug.LogWarning("Is the problem here?");
        cellGrid = new NativeArray<int>(gridBoundsXZ * gridBoundsY * gridBoundsXZ, Allocator.Persistent);
        newCellGrid = new NativeArray<int>(gridBoundsXZ * gridBoundsY * gridBoundsXZ, Allocator.Persistent);
        // ==================================

        if (randomizeOnStart)
        {
            randomizeCells();
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

    
    
    [EButton.BeginHorizontal("Cell Level"), EButton]
    void randomizeCells()
    {
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {
            //cellGrid[i] = UnityEngine.Random.Range(0, 101);
            cellGrid[i] = UnityEngine.Random.Range(0, 21)*5;
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
        for (int i = 0; i < gridBoundsXZ * gridBoundsY * gridBoundsXZ; i++)
        {

            cellGrid[i] = Mathf.Clamp(i % alternateAmount * 100,0,alternateAmountCap);
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
        float timePassed;
        float time = Time.realtimeSinceStartup;


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


        timePassed = Time.realtimeSinceStartup - time;
        if(printDebugTime)
            Debug.Log("Time(ms): " + (timePassed*1000f));
    }




    void SingleDimensionArray()
    {
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
        }
        */


        // Where's the zmax? I need to learn this better.
        //public int to1D(int x, int y, int z) 
        //{
        //    return (z * xMax * yMax) + (y * xMax) + x;
        //}

        //Need to make a version of this with a zMax... or is it built in? I need to learn this better.
        //public int[] to3D(int idx)
        //{
        //    final int z = idx / (xMax * yMax);
        //    idx -= (z * xMax * yMax);
        //    final int y = idx / xMax;
        //    final int x = idx % xMax;
        //    return new int[] { x, y, z };
        //}

    }


    public float GetCellValue(int x, int y, int z)
    {
        return cellGrid[IX(x, y, z)];
    }
}


