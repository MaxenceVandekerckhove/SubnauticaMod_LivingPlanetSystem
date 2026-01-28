using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LivingPlanetSystem.RandomSpawnerModule
{
    public class RSM_PlayerPositionTracker : MonoBehaviour
    {
        private const float UPDATE_INTERVAL = 5f;
        private float timer = 0f;

        private void Start()
        {
            Plugin.Log.LogInfo("[RSM_PlayerPositionTracker] Started.");
        }

        private void Update()
        {
            if (Player.main == null)
            {
                return;
            }

            timer += Time.deltaTime;

            if (timer < UPDATE_INTERVAL)
                return;

            timer = 0f;

            Vector3 playerPos = Player.main.transform.position;

            Plugin.Log.LogInfo($"[RSM_PlayerPositionTracker] Player Position → X:{playerPos.x:F1} Y:{playerPos.y:F1} Z:{playerPos.z:F1}");
        }
    }
}
