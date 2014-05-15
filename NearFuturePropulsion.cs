 /// NearFuturePropulsion.cs
/// -------------------------
/// Core class for NearFuturePropulsion. 
/// Currently does nothing more than store GUI data
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace NearFuturePropulsion
{
    
    ///  Should only be needed in flight
    [KSPAddon(KSPAddon.Startup.Flight, false)] 
    public class NearFuturePropulsion : MonoBehaviour
    {

        public void Awake()
        {
            // Load resources up
           // Resources.LoadResources();
        }
    }
}
