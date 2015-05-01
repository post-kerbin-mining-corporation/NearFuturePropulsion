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
    class NFPAnimateHeat : PartModule
    {
        [KSPField(isPersistant = false)]
        public string HeatAnimation;
        [KSPField(isPersistant = false)]
        public string HeatTransformName;

        private AnimationState[] heatStates;
        private Transform heatTransform;

        public override void OnStart(PartModule.StartState state)
        {
            heatStates = Utils.SetUpAnimation(HeatAnimation, this.part);
            heatTransform = part.FindModelTransform(HeatTransformName);
            //Debug.Log(heatTransform);
            foreach (AnimationState heatState in heatStates)
            {
                heatState.AddMixingTransform(heatTransform);
                heatState.blendMode = AnimationBlendMode.Blend;
                heatState.layer = 15;
                heatState.weight = 1.0f;
                heatState.enabled = true;
            }
        }

        public void FixedUpdate()
        {

            double heatPercent = part.temperature / part.maxTemp;
            foreach (AnimationState state in heatStates)
            {
                state.normalizedTime = (float)heatPercent;
            }
        }
    }
}