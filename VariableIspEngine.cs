using KSPAPIExtensions;
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
    public class VariableISPEngine:PartModule
    {

        // Use the direct throttle method
        [KSPField(isPersistant = false)]
        public bool UseDirectThrottle = false;

        // Link all engines
        [KSPField(isPersistant = true)]
        public bool LinkAllEngines = false;


        // Name of the engine to tweak
        [KSPField(isPersistant = false)]
        public string EngineID;

        [KSPField(isPersistant = false)]
        public FloatCurve ThrustCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve IspCurve = new FloatCurve();

        // Ec to use
        [KSPField(isPersistant = false)]
        public float EnergyUsage = 100f;

        // Current thrust setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Power", guiFormat = "S2", guiUnits = "%")]
        [UI_FloatEdit(scene = UI_Scene.All, minValue = 0.0f, maxValue = 100.0f, incrementLarge = 25.0f, incrementSmall = 5f, incrementSlide = 0.001f)]
        public float CurThrustSetting = 0f;

        [KSPEvent(guiActive = true, guiName = "Link All Variable Engines", active = true)]
        public void LinkEngines()
        {
            foreach (VariableISPEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = true;
            }
            LinkAllEngines = true;
        }
        
        [KSPEvent(guiActive = true, guiName = "Unlink All Variable Engines", active = false)]
        public void UnlinkEngines()
        {
            foreach (VariableISPEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = false;
            }
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
            foreach (VariableISPEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = !variableEngine.LinkAllEngines;
            }
 
        }


        public override string GetInfo()
        {
            return String.Format("Maximum Thrust: {0:F1} kN", ThrustCurve.Evaluate(0f));// + "\n" +
                //String.Format("Isp at Maximum Thrust: {0:F0} s", MaxThrustIsp) + "\n";
        }

        private float minThrust= 0f;

        private ModuleEnginesFX engine;
        private Propellant ecPropellant;
        private Propellant fuelPropellant;

        private FloatCurve AtmoThrustCurve;
        private FloatCurve AtmoIspCurve;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.moduleName = "Variable ISP Engine";
        }

        public void ChangeIspAndThrust(float level)
        {
            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, IspCurve.Evaluate(level));

            engine.maxThrust = ThrustCurve.Evaluate(level);

            RecalculateRatios(ThrustCurve.Evaluate(level), IspCurve.Evaluate(level));
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

            // Get the appropriate ModuleEnginesFX
            PartModuleList pml = this.part.Modules;
            for (int i = 0; i < pml.Count; i++)
            {
                PartModule curModule = pml.GetModule(i);
                ModuleEnginesFX candidate  = curModule.GetComponent<ModuleEnginesFX>();
                if (candidate.engineID == EngineID)
                {
                    engine = candidate;
                }
            }

            if (engine != null)
                Debug.Log("NFPP: Engine Check Passed");
        

            // Set up the correct propellants
            foreach (Propellant prop in engine.propellants)
            {
                if (prop.name == "ElectricCharge")
                    ecPropellant = prop;
                else
                    fuelPropellant = prop;
            }

            // Choose throttle mode
            if (UseDirectThrottle)
            {
                ChangeIspAndThrust(engine.requestedThrottle);
                Debug.Log("NFP: Using direct throttle VASIMR method");
            }
            else
            {
                ChangeIspAndThrust(CurThrustSetting / 100f);
                Debug.Log("NFP: Using tweakable VASIMR method");
            }
            if (state != StartState.Editor)
                SetupVariableEngines();


            CalculateCurves();

            Debug.Log("NFPP: Variable ISP engine setup complete");
           
        }

        private void CalculateCurves()
        {
            AtmoThrustCurve = new FloatCurve();
            AtmoThrustCurve.Add(0f, engine.maxThrust);
            AtmoThrustCurve.Add(1f, 0f);

            AtmoIspCurve = new FloatCurve();
            AtmoIspCurve.Add(0f, engine.atmosphereCurve.Evaluate(0f));

            float rate = FindFlowRate(engine.maxThrust, engine.atmosphereCurve.Evaluate(0f), fuelPropellant);

            AtmoIspCurve.Add(1f, FindIsp(minThrust, rate, fuelPropellant));
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

        public void ChangeIspAndThrustLinked(VariableISPEngine other, float level)
        {
            if (this != other && CurThrustSetting != level*100f)
                CurThrustSetting = level * 100f;
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
                     if (throttleAmt != lastThrottle)
                     {
                         ChangeIspAndThrust(throttleAmt);
                         
                         lastThrottle = throttleAmt;
                     }
                     CurThrustSetting = engine.requestedThrottle * 100f;
                }
                else
                {
                   
                    if (CurThrustSetting != lastThrustSetting)
                    {
                        ChangeIspAndThrust(CurThrustSetting / 100f);
                        lastThrustSetting = CurThrustSetting;
                        if (LinkAllEngines)
                        {
                            foreach (VariableISPEngine variableEngine in allVariableEngines)
                            {
                                variableEngine.ChangeIspAndThrustLinked(this,CurThrustSetting / 100f);
                            }
                        }
                    }

                }

                if (frameCounter > 10)
                {
                    engine.maxThrust = AtmoThrustCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position));
                    engine.atmosphereCurve = new FloatCurve();
                    engine.atmosphereCurve.Add(0f, AtmoIspCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position)));
                    frameCounter = 0;
                }
                frameCounter ++;
            }
            
        }

    }
}
