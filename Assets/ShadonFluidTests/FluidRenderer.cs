using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidRenderer : MonoBehaviour
{
    public FluidSim fluidSim;
    public GameObject prefab;

    public Transform[,,] renderGrid;

    public bool ScaleByWater = false;
    public bool MakeRenderBoxes = true;

    // Start is called before the first frame update
    void Start() 
    {
        Debug.Log("Starting Render Sim(Replace with single array");
        renderGrid = new Transform[fluidSim.gridBoundsXZ, fluidSim.gridBoundsY, fluidSim.gridBoundsXZ];
        for (int x = 0; x < fluidSim.gridBoundsXZ; x++)
            for (int y = 0; y < fluidSim.gridBoundsY; y++)
                for (int z = 0; z < fluidSim.gridBoundsXZ; z++)
                {
                    if(MakeRenderBoxes)
                        renderGrid[x,y,z] = Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity, this.transform).transform;
                }
                    
        
    }

    // Update is called once per frame
    void Update()
    {
        if(ScaleByWater)
            ScaleWaterByValue();
    }

    void ScaleWaterByValue()
    {
        if (!MakeRenderBoxes)
            return;

        float waterValue = 0; // Reducing how often we create stuff. No idea if this noticable helps though.
        for (int x = 0; x < fluidSim.gridBoundsXZ; x++)
            for (int y = 0; y < fluidSim.gridBoundsY; y++)
                for (int z = 0; z < fluidSim.gridBoundsXZ; z++)
                {
                    waterValue = fluidSim.GetCellValue(x, y, z);
                    if (waterValue == 0)
                    {
                        renderGrid[x, y, z].localScale = Vector3.zero;
                        renderGrid[x, y, z].gameObject.SetActive(false);


                    }
                        
                    else
                    {
                        if(!renderGrid[x,y,z].gameObject.activeInHierarchy)
                            renderGrid[x, y, z].gameObject.SetActive(true);

                        renderGrid[x, y, z].localScale = new Vector3(1, waterValue / fluidSim.maxDensity, 1);
                    }
                        
                }
    }
}


