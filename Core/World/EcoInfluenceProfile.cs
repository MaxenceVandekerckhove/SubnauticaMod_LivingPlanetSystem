using LivingPlanetSystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LivingPlanetSystem.Core.World
{
    // Represents the "ecological context" around the player
    public class EcoInfluenceProfile
    {
        public Dictionary<string, float> BiomeWeights = new Dictionary<string, float>();
        public float AverageDepth;
        public DepthBand DominantDepthBand;
        public float DangerLevel;

        public override string ToString()
        {
            string biomes = string.Join(",", BiomeWeights);
            return $"Biomes[{biomes}] | AvgDepth:{AverageDepth:F1} | Band:{DominantDepthBand} | Danger:{DangerLevel:F2}";
        }
    }
}
