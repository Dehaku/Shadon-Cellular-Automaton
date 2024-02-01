using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FluidDensityText : MonoBehaviour
{
    public FluidSim fluidSim;
    public TMP_Text text;
    public bool updateTextEveryFrame = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (updateTextEveryFrame)
            UpdateText();
            
    }

    [EButton]
    public void UpdateText()
    {
        text.text = "Total Density: " + fluidSim.SumOfDensity();
    }
}
