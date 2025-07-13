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
}