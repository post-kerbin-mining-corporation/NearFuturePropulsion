/// VariableIspEngine
/// ---------------------------------------------------
/// A module that allows the Isp and thrust of an engine to be varied via a GUI
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace NearFuturePropulsion
{
    class VariableISPEngine:PartModule
    {

        // Use the direct throttle method
        [KSPField(isPersistant = false)]
        public bool UseDirectThrottle = false;

        // Link all engines
        [KSPField(isPersistant = true)]
        public bool LinkAllEngines = false;
       
        // Maximum thrust
        [KSPField(isPersistant = false)]
        public float MaxThrust;

        // Isp at maximum thrust
        [KSPField(isPersistant = false)]
        public float MaxThrustIsp;

        // Minimum thrust
        [KSPField(isPersistant = false)]
        public float MinThrust;

        // Isp at minimum thrust
        [KSPField(isPersistant = false)]
        public float MinThrustIsp;

        // Name of the fuel
        [KSPField(isPersistant = false)]
        public string FuelName;

        // Ec to use
        [KSPField(isPersistant = false)]
        public float EnergyUsage = 100f;

        // Current thrust setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Variable Thrust Level"), UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 0.1f)]
        public float CurThrustSetting = 0f;

        [KSPEvent(guiActive = true, guiName = "Link All Variable Engines", active = true)]
        public void LinkEngines()
        {
            LinkAllEngines = true;
        }
        // Retract all radiators attached to this reactor
        [KSPEvent(guiActive = true, guiName = "Unlink All Variable Engines", active = false)]
        public void UnlinkEngines()
        {
            LinkAllEngines = false;
        }

        // Actions
        [KSPAction("Link Engines")]
        public void LinkEnginesAction(KSPActionParam param)
        {
            LinkEngines();
        }

        [KSPAction("Unlink Engines")]
        public void UnlinkEnginesAction(KSPActionParam param)
        {
            UnlinkEngines();
        }

        [KSPAction("Toggle Link Engines")]
        public void ToggleLinkEnginesAction(KSPActionParam param)
        {
            LinkAllEngines = !LinkAllEngines;
        }


        public override string GetInfo()
        {
            return String.Format("Maximum Thrust: {0:F1} kN", MaxThrust) + "\n" +
                String.Format("Isp at Maximum Thrust: {0:F0} s", MaxThrustIsp) + "\n";
        }

        private float minThrust= 0f;

        private ModuleEnginesFX engine;
        private Propellant ecPropellant;
        private Propellant fuelPropellant;


        private FloatCurve ThrustCurve;
        private FloatCurve AtmoCurve;

       

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.moduleName = "Variable ISP Engine";
        }

        public void ChangeIspAndThrust(float level)
        {
            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, Mathf.Lerp(MinThrustIsp ,MaxThrustIsp,level));

        

            engine.maxThrust = Mathf.Lerp(MinThrust, MaxThrust, level);

            RecalculateRatios(engine.maxThrust, Mathf.Lerp(MinThrustIsp, MaxThrustIsp, level));
        }

        private void RecalculateRatios(float desiredthrust, float desiredisp)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((desiredthrust * 1000f) / (desiredisp * 9.82d)) / (fuelDensity*1000f);
            float ecRate = EnergyUsage / (float)fuelRate;

            fuelPropellant.ratio = 0.1f;
            ecPropellant.ratio = fuelPropellant.ratio * ecRate;

            CalculateCurves();


        }

        public override void OnStart(PartModule.StartState state)
        {
            if (state != StartState.Editor)
                started = true;
            else
                started = false;
            // Get moduleEngines
            PartModuleList pml = this.part.Modules;
            for (int i = 0; i < pml.Count; i++)
            {
                PartModule curModule = pml.GetModule(i);
                engine  = curModule.GetComponent<ModuleEnginesFX>();
            }

            if (engine != null)
                Debug.Log("NFPP: Engine Check Passed");

            foreach (Propellant prop in engine.propellants)
            {
                if (prop.name == FuelName)
                    fuelPropellant = prop;
                if (prop.name == "ElectricCharge")
                    ecPropellant = prop;
            }

            


            if (UseDirectThrottle)
                ChangeIspAndThrust(engine.requestedThrottle);
            else
                ChangeIspAndThrust(CurThrustSetting / 100f);

            if (state != StartState.Editor)
                SetupVariableEngines();


            CalculateCurves();

            Debug.Log("NFPP: Variable ISP engine setup complete");
            if (UseDirectThrottle)
            {
                Debug.Log("NFPP: Using direct throttle method");
            }
        }

        private void CalculateCurves()
        {
            ThrustCurve = new FloatCurve();
            ThrustCurve.Add(0f, engine.maxThrust);
            ThrustCurve.Add(1f, 0f);

            AtmoCurve = new FloatCurve();
            AtmoCurve.Add(0f, engine.atmosphereCurve.Evaluate(0f));

            float rate = FindFlowRate(engine.maxThrust, engine.atmosphereCurve.Evaluate(0f), fuelPropellant);

            AtmoCurve.Add(1f, FindIsp(minThrust, rate, fuelPropellant));
        }

        // finds the flow rate given thrust, isp and the propellant 
        private float FindFlowRate(float thrust, float isp, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((thrust * 1000f) / (isp * 9.82d)) / (fuelDensity * 1000f);
            return (float)fuelRate;
        }

        private float FindIsp(float thrust, float flowRate, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double isp = (((thrust * 1000f) / (9.82d)) / flowRate) / (fuelDensity * 1000f);
            return (float)isp;
        }


        private void SetupVariableEngines()
        {
            allVariableEngines = new List<VariableISPEngine>();

            List<Part> allParts = this.vessel.parts;
            foreach (Part pt in allParts)
            {

                PartModuleList pml = pt.Modules;
                for (int i = 0; i < pml.Count; i++)
                {
                    PartModule curModule = pml.GetModule(i);
                    VariableISPEngine candidate = curModule.GetComponent<VariableISPEngine>();

                    if (candidate != null && candidate != this && !allVariableEngines.Contains(candidate ) )
                        allVariableEngines.Add(candidate);
                }

            }
        }

        int frameCounter = 0;
        float lastThrottle = -1f;
        float lastThrustSetting = -1f;

        List<VariableISPEngine> allVariableEngines;

        public void ChangeIspAndThrustLinked(float level)
        {
            //ChangeIspAndThrust(level);
            ResetFrameCount();
            LinkAllEngines = true;
            CurThrustSetting = level * 100f;
        }

        public void ResetFrameCount()
        {
          //  frameCounter = 0;
        }

        public override void OnUpdate()
        {
            if ((LinkAllEngines && Events["LinkEngines"].active) || (!LinkAllEngines && Events["UnlinkEngines"].active))
            {
                Events["LinkEngines"].active = !LinkAllEngines;
                Events["UnlinkEngines"].active = LinkAllEngines;
            }
        }

        bool started = false;

        public void FixedUpdate()
        {
            if (engine != null && started)
            {
                if (UseDirectThrottle)
                {
                    float throttleAmt = engine.requestedThrottle;
                    Debug.Log(throttleAmt);
                     if (throttleAmt != lastThrottle)
                     {
                         ChangeIspAndThrust(throttleAmt);
                         
                         lastThrottle = throttleAmt;
                     }
                     CurThrustSetting = engine.requestedThrottle * 100f;
                }
                else
                {
                    if (LinkAllEngines)
                    {
                        //Debug.Log("NFPP: VariableEngineCount is " + allVariableEngines.Count.ToString());
                        foreach (VariableISPEngine variableEngine in allVariableEngines)
                        {
                            variableEngine.ChangeIspAndThrustLinked(CurThrustSetting / 100f);
                        }
                    }

                    frameCounter++;
                    if (frameCounter >= 10)
                    {
                        if (CurThrustSetting != lastThrustSetting)
                        {
                            ChangeIspAndThrust(CurThrustSetting / 100f);
                            lastThrustSetting = CurThrustSetting;
                        }
                       
                        frameCounter = 0;
                    }
                }

                engine.maxThrust = ThrustCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position));
                engine.atmosphereCurve = new FloatCurve();
                engine.atmosphereCurve.Add(0f, AtmoCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position)));
            }
            
        }

    }
}
