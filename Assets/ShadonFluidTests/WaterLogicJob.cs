using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct WaterLogicJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> originalCellGrid;
    [ReadOnly] public int gridBoundsXZ;
    [ReadOnly] public int gridBoundsY;
    [ReadOnly] public float Gravity;
    [ReadOnly] public int FlowRate;
    [ReadOnly] public int maxDensity;
    [ReadOnly] public bool upDown;
    [ReadOnly] public bool leftRight;
    [ReadOnly] public bool forwardBack;

    int to3dCache;

    public NativeArray<int> outputCellGrid; // write only?

    public int IX(int x, int y, int z) 
    {
        return x + y * gridBoundsXZ + z * gridBoundsXZ * gridBoundsY;
    }

    
    public int3 to3D(int idx)
    {
        int z = idx / (gridBoundsXZ * gridBoundsY);
        idx -= (z * gridBoundsXZ * gridBoundsY);
        int y = idx / gridBoundsXZ;
        int x = idx % gridBoundsXZ;
        return new int3(x, y, z);
    }

    // public int[] to3D(int idx)
        //{
        //    final int z = idx / (xMax * yMax);
        //    idx -= (z * xMax * yMax);
        //    final int y = idx / xMax;
        //    final int x = idx % xMax;
        //    return new int[] { x, y, z };
        //}


    public void Execute(int index)
    {
        int3 to3DID; // TODO: Replace the function to 'out' the x,y,z coordinates, save an allocation and getx/y/z (burst might already be doing this?)
        int x;
        int y;
        int z;

        int thisCell;
        int cellUp;
        int cellDown;
        int thisCellDensity;
        int densityToGive;
        

                
        to3DID = to3D(index);
        x = to3DID[0];
        y = to3DID[1];
        z = to3DID[2];

        // Fix border cases eventually.
        
        

        thisCell = index; // Replace thisCell with a reference to the cell itself?
        thisCellDensity = originalCellGrid[thisCell];
        cellUp = IX(x, y + 1, z);
        cellDown = IX(x, y - 1, z);

        int outputCellDensity = thisCellDensity;
        densityToGive = thisCellDensity;




        if(upDown)
        {
            if (thisCellDensity > 0)
            {
                if (y > 0)
                {
                    if (originalCellGrid[cellDown] < maxDensity)
                    {
                        outputCellDensity -= FlowRate;
                        densityToGive -= 1;
                    }
                }
            }

            if (thisCellDensity < maxDensity)
            {
                if (y < gridBoundsY - 1) // -1 because we aim at the cell above us, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[cellUp] > 1)
                    {
                        outputCellDensity += FlowRate; // Only affect current cell, read neighbor cells.
                    }
                }
            }

            

            
        }

        



        if (leftRight)
        {
            // Left, // lose to right, gain from left

            if (densityToGive > 0)
            {
                if (x > 0)
                {
                    int leftCellDensity = originalCellGrid[IX(x - 1, y, z)];
                    if (leftCellDensity < maxDensity && leftCellDensity < outputCellDensity)
                    {
                        outputCellDensity -= 1;
                        densityToGive -= 1;
                    }
                }
            }


            if (thisCellDensity < maxDensity)
            {
                if (x < gridBoundsXZ - 1) // -1 because we aim at the next cell, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[IX(x+1, y, z)] > outputCellDensity) 
                    {
                        outputCellDensity += 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }

            

            /*
            // Right, // Gain from left, lose to right
            if (thisCellDensity > 0)
            {
                if (x > 0)
                {
                    int leftCellDensity = originalCellGrid[IX(x - 1, y, z)];
                    if (leftCellDensity < maxDensity && leftCellDensity < outputCellDensity)
                    {
                        outputCellDensity += 1;
                    }
                }
            }
            if (thisCellDensity < maxDensity)
            {
                if (x < gridBounds - 1) // -1 because we aim at the next cell, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[IX(x + 1, y, z)] > outputCellDensity)
                    {
                        outputCellDensity -= 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }
            */





        }


        outputCellGrid[thisCell] = outputCellDensity;
    }
}

