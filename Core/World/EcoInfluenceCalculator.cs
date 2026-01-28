using LivingPlanetSystem.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace LivingPlanetSystem.Core.World
{
    // Analyze the cells around the player and produce a weighted ecological profile.
    public static class EcoInfluenceCalculator
    {
        private const int CELL_RADIUS = 3; // Nombre de cellules autour du joueur à considérer
        private const float CELL_SIZE = 50f;

        public static EcoInfluenceProfile Calculate(Vector3 playerPos)
        {
            EcoInfluenceProfile profile = new EcoInfluenceProfile();

            float totalWeight = 0f;
            float depthAccum = 0f;

            int centerX = Mathf.FloorToInt(playerPos.x / CELL_SIZE);
            int centerZ = Mathf.FloorToInt(playerPos.z / CELL_SIZE);

            for (int dx = -CELL_RADIUS; dx <= CELL_RADIUS; dx++)
            {
                for (int dz = -CELL_RADIUS; dz <= CELL_RADIUS; dz++)
                {
                    int cellX = centerX + dx;
                    int cellZ = centerZ + dz;

                    // Centre de la cellule
                    Vector3 cellCenter = new Vector3(
                        (cellX + 0.5f) * CELL_SIZE,
                        playerPos.y,
                        (cellZ + 0.5f) * CELL_SIZE
                    );

                    EcoCell cell = EcoCellManager.Instance.GetEcoCell(cellCenter);

                    // Distance à la cellule centrale
                    float dist = Vector2.Distance(new Vector2(centerX, centerZ), new Vector2(cellX, cellZ));
                    float weight = 1f / (1f + dist);

                    totalWeight += weight;
                    depthAccum += cell.Depth * weight;

                    // Normaliser le nom du biome pour éviter les doublons
                    string biomeKey = cell.Biome.Trim().ToLowerInvariant();

                    if (!profile.BiomeWeights.ContainsKey(biomeKey))
                        profile.BiomeWeights[biomeKey] = 0f;

                    profile.BiomeWeights[biomeKey] += weight;
                }
            }

            // Normalisation des poids
            List<string> keys = new List<string>(profile.BiomeWeights.Keys);
            foreach (var key in keys)
                profile.BiomeWeights[key] /= totalWeight;

            // Profondeur moyenne
            profile.AverageDepth = depthAccum / totalWeight;

            // DepthBand dominant
            profile.DominantDepthBand = LPS_DepthUtils.GetDepthBand(profile.AverageDepth);

            // DangerLevel simpliste pour l'instant
            profile.DangerLevel = Mathf.Clamp01(profile.AverageDepth / 300f);

            Plugin.Log.LogInfo($"[EcoInfluenceCalculator] Profile computed → {profile}");

            return profile;
        }
    }
}
