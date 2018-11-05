/// NFPAnimateHeat
/// ---------------------------
/// Plays a second animation. Replaces ModuleAnimateHeat, because it can't handle 2 animations on one part
/// Kinda a unity hack.
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NearFuturePropulsion
{
    public class ModuleRCSEmissive : PartModule
    {
        [KSPField(isPersistant = false)]
        public FloatCurve alphaCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve blueCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve greenCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve redCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public string shaderColorParameter = "_EmissiveColor";

        ModuleRCSFX rcs;
        List<Material> thrustMaterials;

        public void Start()
        {
            rcs = part.GetComponent<ModuleRCSFX>();

            thrustMaterials = new List<Material>();
            foreach (Transform t in rcs.thrusterTransforms)
            {
                thrustMaterials.Add(t.GetComponentInChildren<MeshRenderer>().material);
            }
        }

        public void FixedUpdate()
        {

            if (HighLogic.LoadedSceneIsFlight)
            {
                for (int i = 0; i < thrustMaterials.Count; i++)
                {
                    Color c;
                    c = new Color(redCurve.Evaluate(rcs.thrustForces[i]),
                                  greenCurve.Evaluate(rcs.thrustForces[i]),
                                  blueCurve.Evaluate(rcs.thrustForces[i]),
                                  alphaCurve.Evaluate(rcs.thrustForces[i]));
                    thrustMaterials[i].SetColor(shaderColorParameter, c);

                }
            }
        }
    }
}