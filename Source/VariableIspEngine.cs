/// VariableIspEngine
/// ---------------------------------------------------
/// A module that allows the Isp and thrust of an engine to be varied via a GUI
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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

        [KSPField(isPersistant = false)]
        public FloatCurve ThrustCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve IspCurve = new FloatCurve();

 
        // Current thrust setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Power Level") , UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
        public float CurThrustSetting = 0f;

        // Ec to use
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Power Input", guiUnits = " Ec/s")]
        public float EnergyUsage = 100f;    

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Estimated Isp:", guiUnits = " s")]
        public float CurIsp;
        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Estimated Thrust:", guiUnits = " kN")]
        public float CurThrust;

        [KSPField(isPersistant = true)]
        public int EngineModeID;

        // MODE 1 PARAMS
        [KSPField(isPersistant = false)]
        public string Mode1Name;
        [KSPField(isPersistant = false)]
        public string Mode1Propellant;
        [KSPField(isPersistant = false)]
        public float Mode1ThrustMin;
        [KSPField(isPersistant = false)]
        public float Mode1ThrustMax;
        [KSPField(isPersistant = false)]
        public float Mode1IspMin;
        [KSPField(isPersistant = false)]
        public float Mode1IspMax;
        [KSPField(isPersistant = false)]
        public string Mode1Animation = "";

        // MODE 2 PARAMS
        [KSPField(isPersistant = false)]
        public string Mode2Name;
        [KSPField(isPersistant = false)]
        public string Mode2Propellant;
        [KSPField(isPersistant = false)]
        public float Mode2ThrustMin;
        [KSPField(isPersistant = false)]
        public float Mode2ThrustMax;
        [KSPField(isPersistant = false)]
        public float Mode2IspMin;
        [KSPField(isPersistant = false)]
        public float Mode2IspMax;
        [KSPField(isPersistant = false)]
        public string Mode2Animation= "";

        // Fuel Status string
        [KSPField(isPersistant = false, guiActive = true, guiName = "Fuel Mode")]
        public string CurrentEngineID = "";

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
          //  return String.Format("Maximum Thrust: {0:F1} kN", ThrustCurve.Evaluate(1f)) + "\n" +
          //      String.Format("Isp at Maximum Thrust: {0:F0} s", IspCurve.Evaluate(1f)) + "\n";
            return "";
        }

        private float minThrust= 0f;

        private MultiModeEngine multiEngine;
        private ModuleEnginesFX engine;
        private List<ModuleEnginesFX> engines;

        private Propellant ecPropellant;
        private Propellant fuelPropellant;

        private FloatCurve AtmoThrustCurve;
        private FloatCurve AtmoIspCurve;
        
        private VariableEngineMode[] engineModes;
       
        public class VariableEngineMode
        {
            public string name = "";
            public string propellant = "";
            public FloatCurve thrustCurve = new FloatCurve();
            public FloatCurve ispCurve = new FloatCurve();
            public AnimationState[] throttleAnim;

            public VariableEngineMode() {}

            public VariableEngineMode(Part p, string n, string prop,float t1, float t2, float i1, float i2, string anim )
            {
                name = n;
                propellant = prop;
                thrustCurve = new FloatCurve();
                
                thrustCurve.Add(0f, t1,0f,0f);
                thrustCurve.Add(1f, t2,0f,0f);
                ispCurve = new FloatCurve();
                ispCurve.Add(0f, i1,0f,0f);
                ispCurve.Add(1f, i2,0f,0f);
                throttleAnim = Utils.SetUpAnimation(anim,p);

                foreach (AnimationState t in throttleAnim)
                {
                   // t.AddMixingTransform("Engine");
                    t.blendMode = AnimationBlendMode.Blend;
                    t.layer = 15;
                    t.weight = 1.0f;
                    t.enabled = true;
                }
            }
        }

    

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.moduleName = "Variable ISP Engine";
        }



        public void ChangeIspAndThrust(float level)
        {

            RecalculateRatios(ThrustCurve.Evaluate(level), IspCurve.Evaluate(level));

            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, IspCurve.Evaluate(level));
            engine.atmosphereCurve.Add(1f, 100f);
            engine.atmosphereCurve.Add(4f, 1f);

            engine.maxThrust = ThrustCurve.Evaluate(level);

            CurIsp = engine.atmosphereCurve.Evaluate(0f);
            CurThrust = engine.maxThrust = ThrustCurve.Evaluate(level);
            //Utils.Log("VariableIspEngine: Changed Isp toengine.atmosphereCurve.Evaluate(0f) " + engine.atmosphereCurve.Evaluate(0f).ToString());
            //Utils.Log("VariableIspEngine: Changed thrust to " + engine.maxThrust.ToString());            
        }

        private void RecalculateRatios(float desiredthrust, float desiredisp)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((desiredthrust ) / (desiredisp * Utils.GRAVITY)) ;
            engine.maxFuelFlow = (float)fuelRate;
            fuelRate = fuelRate / fuelDensity;
            float ecRate = EnergyUsage / (float)fuelRate;
            

            fuelPropellant.ratio = 0.1f;
            ecPropellant.ratio = fuelPropellant.ratio * ecRate;

            //CalculateCurves();
        }

        public override void OnStart(PartModule.StartState state)
        {

            if (state != StartState.Editor)
                SetupVariableEngines();


            LoadEngineModes();
            LoadEngineModules();

            if (engines.Count == 0)
            {
                Utils.Log("VariableIspEngine: Engine modules not good");
                return;
            }

            SetMode();

            if (engine != null)
                Utils.Log("VariableIspEngine: Engine module check passed");

           

            // Choose throttle mode
            if (UseDirectThrottle)
            {
                ChangeIspAndThrust(engine.requestedThrottle);
                Utils.Log("VariableIspEngine: Using direct throttle method");
            }
            else
            {
                ChangeIspAndThrust(CurThrustSetting / 100f);
                Utils.Log("VariableIspEngine: Using tweakable method");
            }
            

            CalculateCurves();

            Utils.Log("VariableIspEngine: Setup complete");
           
        }

        // Finds the engine modes
        protected void LoadEngineModes()
        {
            engineModes = new VariableEngineMode[2];
            engineModes[0] = new VariableEngineMode(this.part,Mode1Propellant,Mode1Name,Mode1ThrustMin,Mode1ThrustMax,Mode1IspMin,Mode1IspMax,Mode1Animation);
            engineModes[1] = new VariableEngineMode(this.part,Mode2Propellant,Mode2Name, Mode2ThrustMin, Mode2ThrustMax, Mode2IspMin, Mode2IspMax,Mode2Animation);
        }


        // Finds multiengine and ModuleEnginesFX 
        private void LoadEngineModules()
        {
            engines = new List<ModuleEnginesFX>();
            PartModuleList modules = part.Modules;

            foreach (PartModule mod in part.Modules)
            {
                if (mod.moduleName == "ModuleEnginesFX")
                {
                    engines.Add((ModuleEnginesFX)mod);
                    //Utils.Log("VariableIspEngine: " +  ((ModuleEnginesFX)mod).runningEffectName);
                }
                if (mod.moduleName == "MultiModeEngine")
                    multiEngine = mod.GetComponent<MultiModeEngine>();
            }
           
        }
        private void SetMode()
        {

            if (multiEngine.runningPrimary)
                EngineModeID = 0;
            else
                EngineModeID = 1;

            Utils.Log("VariableIspEngine: Changing mode to " + engineModes[EngineModeID].name);
            CurrentEngineID = engineModes[EngineModeID].name;
            engine = engines[EngineModeID];
            ThrustCurve = engineModes[EngineModeID].thrustCurve;
            IspCurve = engineModes[EngineModeID].ispCurve;

            foreach (Propellant prop in engine.propellants)
            {
                if (prop.name != "ElectricCharge")
                {
                    fuelPropellant = prop;
                }
                else
                {
                    ecPropellant = prop;
                }
            }

            //Utils.Log("VariableIspEngine: Changed mode to " + engine.engineID);
            Utils.Log("VariableIspEngine: Fuel: " + fuelPropellant.name);
            Utils.Log("VariableIspEngine: Thrust Curve: " + ThrustCurve.Evaluate(0f) + " to " + ThrustCurve.Evaluate(1f));
            Utils.Log("VariableIspEngine: Isp Curve: " + IspCurve.Evaluate(0f) + " to " + IspCurve.Evaluate(1f));

            AdjustVariableThrust();
        }
       
             

        private void CalculateCurves()
        {
           // AtmoThrustCurve = new FloatCurve();
           // AtmoThrustCurve.Add(0f, engine.maxThrust);
           // AtmoThrustCurve.Add(1f, 0f);

           // AtmoIspCurve = new FloatCurve();
           // AtmoIspCurve.Add(0f, engine.atmosphereCurve.Evaluate(0f));

            

           // float rate = FindFlowRate(engine.maxThrust, engine.atmosphereCurve.Evaluate(0f), fuelPropellant);

            //AtmoIspCurve.Add(1f, FindIsp(minThrust, rate, fuelPropellant));
        }

        // finds the flow rate given thrust, isp and the propellant 
        private float FindFlowRate(float thrust, float isp, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((thrust * 1000f) / (isp * Utils.GRAVITY)) / (fuelDensity * 1000f);
            return (float)fuelRate;
        }

        private float FindIsp(float thrust, float flowRate, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double isp = (((thrust * 1000f) / (Utils.GRAVITY)) / flowRate) / (fuelDensity * 1000f);
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
            if (engine != null && multiEngine.runningPrimary)
            {
                foreach (AnimationState anim in engineModes[0].throttleAnim)
                {
                    anim.normalizedTime = Mathf.MoveTowards(anim.normalizedTime, engine.normalizedThrustOutput, TimeWarp.deltaTime);
                }
                foreach (AnimationState anim in engineModes[1].throttleAnim)
                {
                    anim.normalizedTime = Mathf.MoveTowards(anim.normalizedTime, 0f, TimeWarp.deltaTime * 3.0f);
                }
            }
            else
            {
                foreach (AnimationState anim in engineModes[1].throttleAnim)
                {
                    anim.normalizedTime = Mathf.MoveTowards(anim.normalizedTime, engine.normalizedThrustOutput, TimeWarp.deltaTime);
                }
                foreach (AnimationState anim in engineModes[0].throttleAnim)
                {
                    anim.normalizedTime = Mathf.MoveTowards(anim.normalizedTime, 0f, TimeWarp.deltaTime * 3.0f);
                }
            }
        }

        public void FixedUpdate()
        {

          
            if (engine != null)
            {
                if ((multiEngine.runningPrimary && EngineModeID != 0) || (!multiEngine.runningPrimary && EngineModeID != 1))
                {
                    SetMode();
                }
   
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
                        Utils.Log("VariableIspEngine: Changed power to " + CurThrustSetting.ToString());
                        AdjustVariableThrust();
                    }

                }
                
            }
            
        }

        private void AdjustVariableThrust()
        {
            ChangeIspAndThrust(CurThrustSetting / 100f);
            lastThrustSetting = CurThrustSetting;
            if (LinkAllEngines && allVariableEngines != null)
            {
                foreach (VariableISPEngine variableEngine in allVariableEngines)
                {
                    variableEngine.ChangeIspAndThrustLinked(this, CurThrustSetting / 100f);
                }
            }
        }

    }
}
