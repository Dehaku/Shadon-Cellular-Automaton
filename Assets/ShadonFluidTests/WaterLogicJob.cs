using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// Burst Manual; https://docs.unity3d.com/Packages/com.unity.burst@1.2/manual/index.html

[BurstCompile]
public struct WaterLogicJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> originalCellGrid;
    [ReadOnly] public int gridBoundsXZ;
    [ReadOnly] public int gridBoundsY;
    [ReadOnly] public float Gravity;
    [ReadOnly] public int FlowRate;
    [ReadOnly] public int maxDensity;
    [ReadOnly] public bool down;
    [ReadOnly] public bool up;
    [ReadOnly] public bool left;
    [ReadOnly] public bool right;
    [ReadOnly] public bool forward;
    [ReadOnly] public bool back;

    [WriteOnly] public NativeArray<int> outputCellGrid; // write only?

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

        thisCell = index; // This could probably be cleaner.
        thisCellDensity = originalCellGrid[thisCell];
        cellUp = IX(x, y + 1, z);
        cellDown = IX(x, y - 1, z);

        int outputCellDensity = thisCellDensity;
        densityToGive = thisCellDensity;

        if(down)
        {
            // If we have some, If not Bottom, If room below us, drain ourselves.
            if (densityToGive > 0)
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

            // If we have room, If we're not Top, if above us has some, fill ourselves.
            if (thisCellDensity < maxDensity)
            {
                if (y < gridBoundsY - 1) // -1 because we aim at the cell above us, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[cellUp] > 0)
                    {
                        outputCellDensity += FlowRate; // Only affect current cell, read neighbor cells.
                    }
                }
            }
        }

        if(up)
        {
            // TODO: Needs data struct with pressure and stuff.
        }


        if (right)
        {
            // Flowing Right; lose to right, gain from left
            if (densityToGive > 0)
            {
                if (x < gridBoundsXZ - 1)
                {
                    int rightCellDensity = originalCellGrid[IX(x + 1, y, z)];
                    if (rightCellDensity < maxDensity && rightCellDensity < thisCellDensity)
                    {
                        outputCellDensity -= 1;
                        densityToGive -= 1;
                    }
                }
            }
            if (thisCellDensity < maxDensity)
            {
                if (x > 0) // -1 because we aim at the next cell, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[IX(x-1, y, z)] > thisCellDensity) 
                    {
                        outputCellDensity += 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }
        }

        if(left)
        {
            // Flowing Left; lose to left, gain from right
            if (densityToGive > 0)
            {
                if (x > 0)
                {
                    int leftCellDensity = originalCellGrid[IX(x - 1, y, z)];
                    if (leftCellDensity < maxDensity && leftCellDensity < thisCellDensity)
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
                    if (originalCellGrid[IX(x+1, y, z)] > thisCellDensity) 
                    {
                        outputCellDensity += 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }
            
        }

        if (forward)
        {
            // Flowing Forward; lose to forward, gain from back
            if (densityToGive > 0)
            {
                if (z < gridBoundsXZ - 1)
                {
                    int forwardCellDensity = originalCellGrid[IX(x, y, z + 1)];
                    if (forwardCellDensity < maxDensity && forwardCellDensity < thisCellDensity)
                    {
                        outputCellDensity -= 1;
                        densityToGive -= 1;
                    }
                }
            }
            if (thisCellDensity < maxDensity)
            {
                if (z > 0) // -1 because we aim at the next cell, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[IX(x, y, z - 1)] > thisCellDensity)
                    {
                        outputCellDensity += 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }
        }

        if (back)
        {
            // Flowing Back; lose to back, gain from forward
            if (densityToGive > 0)
            {
                if (z > 0)
                {
                    int backCellDensity = originalCellGrid[IX(x, y, z - 1)];
                    if (backCellDensity < maxDensity && backCellDensity < thisCellDensity)
                    {
                        outputCellDensity -= 1;
                        densityToGive -= 1;
                    }
                }
            }
            if (thisCellDensity < maxDensity)
            {
                if (z < gridBoundsXZ - 1) // -1 because we aim at the next cell, so it's further than just 'less than bounds'
                {
                    if (originalCellGrid[IX(x, y, z + 1)] > thisCellDensity)
                    {
                        outputCellDensity += 1; // Only affect current cell, read neighbor cells.
                    }
                }
            }

        }


        outputCellGrid[thisCell] = outputCellDensity;
    }
}

