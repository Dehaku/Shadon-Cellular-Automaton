using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Unity.Collections;

[BurstCompile]
public class FluidSim : MonoBehaviour
{


    public Vector3Int gridBounds = new Vector3Int();
    public int gridBoundsSingle = 96;
    
    public float[,,] fluidGrid;
    public float[,,] fluidGrid_prev;

    public float[,,] velocityGrid;
    public float[,,] velocityGrid_prev;

    public float[,,] densityGrid;
    public float[,,] densityGrid_prev;

    public float[,,] sourceGrid;

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


    public float[] fluidGridSingle;
    public float[] newFluidGridSingle;

    NativeArray<int> cellGrid, newCellGrid;
    public int jobsPerThread = 64;
    public bool printDebugTime = false;



    // C,L,R,F,B,U,D
    // Center, Left, Right, Forward, Back, Up, Down
    NativeArray<int> cCG, ncCG;
    NativeArray<int> lCG, nlCG;
    NativeArray<int> rCG, nrCG;
    NativeArray<int> fCG, nfCG;
    NativeArray<int> bCG, nbCG;
    NativeArray<int> uCG, nuCG;
    NativeArray<int> dCG, ndCG;



    int IXXX(int x, int y, int z)
    {
        return Mathf.Clamp(x + y * gridBoundsSingle + z * gridBoundsSingle * gridBoundsSingle, 0, gridBoundsSingle * gridBoundsSingle * gridBoundsSingle);
    }

    int IX(int x, int y, int z)
    {
        return x + y * gridBoundsSingle + z * gridBoundsSingle * gridBoundsSingle;
    }


    public int3 to3D(int idx)
    {
        int z = idx / (gridBoundsSingle * gridBoundsSingle);
        idx -= (z * gridBoundsSingle * gridBoundsSingle);
        int y = idx / gridBoundsSingle;
        int x = idx % gridBoundsSingle;
        return new int3(x, y, z);
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
        }
        */




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

        fluidGrid = new float[gridBounds.x, gridBounds.y, gridBounds.z];
        fluidGrid_prev = new float[gridBounds.x, gridBounds.y, gridBounds.z];

        velocityGrid = new float[gridBounds.x, gridBounds.y, gridBounds.z];
        velocityGrid_prev = new float[gridBounds.x, gridBounds.y, gridBounds.z];

        densityGrid = new float[gridBounds.x, gridBounds.y, gridBounds.z];
        densityGrid_prev = new float[gridBounds.x, gridBounds.y, gridBounds.z];

        sourceGrid = new float[gridBounds.x, gridBounds.y, gridBounds.z];

        fluidGridSingle = new float[gridBoundsSingle * gridBoundsSingle * gridBoundsSingle];
        newFluidGridSingle = new float[gridBoundsSingle * gridBoundsSingle * gridBoundsSingle];

        cellGrid = new NativeArray<int>(gridBoundsSingle * gridBoundsSingle * gridBoundsSingle,Allocator.Persistent);
        newCellGrid = new NativeArray<int>(gridBoundsSingle * gridBoundsSingle * gridBoundsSingle, Allocator.Persistent);

        // ==================================









        for (int x = 0; x < gridBounds.x; x++)
            for (int y = 0; y < gridBounds.y; y++)
                for (int z = 0; z < gridBounds.z; z++)
                    fluidGrid[x, y, z] = x;

        if (randomizeOnStart)
        {
            MakeAllWaterCellsRandom();
            for(int i = 0; i < gridBoundsSingle*gridBoundsSingle*gridBoundsSingle; i++)
            {
                fluidGridSingle[i] = UnityEngine.Random.Range(0, 100f);
            }

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
        for (int i = 0; i < gridBoundsSingle * gridBoundsSingle * gridBoundsSingle; i++)
        {
            //cellGrid[i] = UnityEngine.Random.Range(0, 101);
            cellGrid[i] = UnityEngine.Random.Range(0, 21)*5;
        }
    }

    [EButton]
    void iterateCells()
    {
        for (int i = 0; i < gridBoundsSingle * gridBoundsSingle * gridBoundsSingle; i++)
        {

            cellGrid[i] = Mathf.Clamp(1 * (i % 32), 0, 100);
        }
    }

    [EButton]
    void randomizeAndIterateCells()
    {
        for (int i = 0; i < gridBoundsSingle * gridBoundsSingle * gridBoundsSingle; i++)
        {

            cellGrid[i] = Mathf.Clamp(1 * (i % 32), 0, 100) * UnityEngine.Random.Range(1, 3);
        }
    }
    [EButton, EButton.EndHorizontal]
    void alternateCells()
    {
        for (int i = 0; i < gridBoundsSingle * gridBoundsSingle * gridBoundsSingle; i++)
        {

            cellGrid[i] = Mathf.Clamp(i % alternateAmount * 100,0,alternateAmountCap);
        }
    }

    [EButton]
    public int SumOfDensity()
    {
        int totalDensity = 0;
        //int highestDensity = 0;
        for (int i = 0; i < gridBoundsSingle * gridBoundsSingle * gridBoundsSingle; i++)
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
            gridBounds = this.gridBoundsSingle,
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


        if (RunSinglethreadSimulation)
            SingleThreadSimulateSingle();

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
            //SingleThreadSimulateSinglex2();
        {
            /*
            WaterLogicJobV3 waterLogicJob = new WaterLogicJobV3()
            {
                originalCellGrid = cell3dGrid,
                gridBounds = this.gridBoundsSingle,
                Gravity = this.Gravity,
                maxDensity = this.maxDensity,

                outputCellGrid = newCell3dGrid,
            };

            Debug.Log("cell3dgrid.length" + cell3dGrid.Length);
            JobHandle waterLogicJobHandle = waterLogicJob.Schedule(cell3dGrid.Length, jobsPerThread);
            waterLogicJobHandle.Complete();

            newCell3dGrid = waterLogicJob.outputCellGrid;
            /*
            WaterLogicJob waterLogicJob = new WaterLogicJob()
            {
                originalCellGrid = cellGrid,
                gridBounds = this.gridBoundsSingle,
                Gravity = this.Gravity,
                maxDensity = this.maxDensity,

                outputCellGrid = newCellGrid,
            };

            JobHandle waterLogicJobHandle = waterLogicJob.Schedule(cellGrid.Length, jobsPerThread);
            waterLogicJobHandle.Complete();

            newCellGrid = waterLogicJob.outputCellGrid;
            */

            

        }
        //SingleThreadSimulate();


        timePassed = Time.realtimeSinceStartup - time;
        if(printDebugTime)
            Debug.Log("Time(ms): " + (timePassed*1000f));
    }

    [EButton("Simulate 1d Array")]
    void SingleThreadSimulateSingle()
    {
        int index = 0;
        int indexUp = 0;
        //int indexLeft = 0;
        //int indexRight = 0;
        //int indexForward = 0;
        //int indexBack = 0;
        //int indexDown = 0;

        for (int x = 1; x < gridBoundsSingle - 1; x++)
            for (int y = 1; y < gridBoundsSingle - 1; y++)
                for (int z = 1; z < gridBoundsSingle - 1; z++)
                {
                    index = IX(x, y, z);
                    indexUp = IX(x, y + 1, z);

                    if (fluidGridSingle[index] < maxDensity)
                    {
                        if (fluidGridSingle[indexUp] > 1)
                        {
                            fluidGridSingle[indexUp] -= 1;
                            fluidGridSingle[index] += 1;
                        }
                        else if (fluidGridSingle[indexUp] > 0)
                        {
                            fluidGridSingle[index] += fluidGridSingle[indexUp];
                            fluidGridSingle[indexUp] = 0;
                        }
                    }
                }
    }

    void SingleThreadSimulateSinglex2()
    {
        /*

        int index = 0;
        int indexUp = 0;
        //int indexLeft = 0;
        //int indexRight = 0;
        //int indexForward = 0;
        //int indexBack = 0;
        //int indexDown = 0;

        int thisCell;
        int cellUp;
        int cellDown;

        for (int x = 1; x < gridBoundsSingle - 1; x++)
            for (int y = 1; y < gridBoundsSingle - 1; y++)
                for (int z = 1; z < gridBoundsSingle - 1; z++)
                {
                    thisCell = IX(x, y, z);
                    cellUp = IX(x, y + 1, z);
                    cellDown = IX(x, y - 1, z);

                    float thisCellDensity = fluidGridSingle[thisCell];
                    float thisCellOutputDensity = thisCellDensity;

                    if (thisCellDensity < maxDensity)
                    {
                        if (fluidGridSingle[cellUp] > 1)
                        {
                            thisCellOutputDensity += 1; // Only affect current cell, read neighbor cells.
                        }

                    }
                    if (thisCellDensity > 0)
                        if (fluidGridSingle[cellDown] < maxDensity)
                        {
                            thisCellOutputDensity -= 1;
                        }

                    newFluidGridSingle[thisCell] = thisCellOutputDensity;
                }
        fluidGridSingle = newFluidGridSingle;

        */

    }


    [EButton("Simulate 3d Array")]
    void SingleThreadSimulate()
    {
        


                    // Worry about edge cases later.
                    for (int x = 1; x < 96-1; x++)
            for (int y = 1; y < 96-1; y++)
                for (int z = 1; z < 96-1; z++)
                {
                    
                    if(fluidGrid[x, y, z] < maxDensity)
                    {
                        if (fluidGrid[x, y + 1, z] > 1)
                        {
                            fluidGrid[x, y + 1, z] -= 1;
                            fluidGrid[x, y, z] += 1;
                        }
                        else if (fluidGrid[x, y + 1, z] > 0)
                        {
                            fluidGrid[x, y, z] += fluidGrid[x, y + 1, z];
                            fluidGrid[x, y + 1, z] = 0;
                        }
                    }
                        

                        // if (fluidGrid[x, y + 1, z] > fluidGrid[x, y, z])
                        // {
                        //     fluidGrid[x, y + 1, z] -= 1;
                        //     fluidGrid[x, y, z] += 1;
                        // }


                }

                    

    }

    void MultiThreadSimulate()
    {

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
    }


    public float GetCellValue(int x, int y, int z)
    {
        return cellGrid[IX(x, y, z)];
    }

    void PrintGrid()
    {
        for (int x = 0; x < gridBounds.x; x++)
            for (int y = 0; y < gridBounds.y; y++)
                for (int z = 0; z < gridBounds.z; z++)
                {
                    Debug.Log(fluidGrid[x, y, z]);

                }
    }



    [EButton.BeginHorizontal("Water Level"), EButton]
    void MakeAllWaterCellsFull()
    {
        for (int x = 0; x < gridBounds.x; x++)
            for (int y = 0; y < gridBounds.y; y++)
                for (int z = 0; z < gridBounds.z; z++)
                    fluidGrid[x, y, z] = 100;
    }
    [EButton]
    void MakeAllWaterCellsEmpty()
    {
        for (int x = 0; x < gridBounds.x; x++)
            for (int y = 0; y < gridBounds.y; y++)
                for (int z = 0; z < gridBounds.z; z++)
                    fluidGrid[x, y, z] = 0;
    }
    [EButton]
    void MakeAllWaterCellsRandom()
    {
        for (int x = 0; x < gridBounds.x; x++)
            for (int y = 0; y < gridBounds.y; y++)
                for (int z = 0; z < gridBounds.z; z++)
                {
                    fluidGrid[x, y, z] = UnityEngine.Random.Range(0, 100f);
                    //cell3dGrid[x, y, z] = UnityEngine.Random.Range(0, 101);
                }
                    
    }



}


