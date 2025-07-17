using System.Collections.Generic;
using System;
using UnityEngine;
using Random = System.Random;
using System.Diagnostics;
using Object = UnityEngine.Object;

namespace ICanShowYouTheWorld
{
    // ----------- Maps keys to actions ----------------
    public class InputManager
    {
        private readonly Dictionary<KeyCode, Action> mappings = new Dictionary<KeyCode, Action>();

        public void Register(KeyCode key, Action action)
        {
            mappings[key] = action;
        }

        public void HandleInput()
        {
            foreach (var kv in mappings)
            {
                if (Input.GetKeyDown(kv.Key))
                    kv.Value.Invoke();
            }
        }
    }

    [RequireComponent(typeof(Transform))]
    public class GroundFollower : MonoBehaviour
    {
        /// <summary>Who to follow (usually the player)</summary>
        public Transform target;

        /// <summary>How high above the ground to sit</summary>
        public float heightOffset = 0.01f;

        void Update()
        {
            if (target == null) return;

            // raycast from above the target straight down
            Vector3 origin = target.position + Vector3.up * 10f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f))
            {
                // snap to that ground point
                transform.position = hit.point + Vector3.up * heightOffset;
            }
        }
    }

}