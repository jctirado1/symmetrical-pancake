using System;
using System.Collections.Generic;
using UnityEngine;

namespace LondonStreets
{
    /// <summary>
    /// The list of characters and cars the editor found in your project (see
    /// found_assets.txt). It is rewritten automatically by the editor every
    /// time you press Play, so you never need to edit it. It lives in a
    /// Resources folder so it also works in a built game.
    /// </summary>
    public class AssetCatalog : ScriptableObject
    {
        public const string ResourceName = "LondonStreetsCatalog";

        [Serializable]
        public class ModelClips
        {
            public GameObject model;
            public AnimationClip[] clips;
        }

        public GameObject player;
        public List<GameObject> characters = new List<GameObject>();
        public List<GameObject> cars = new List<GameObject>();
        /// <summary>What's in the Characters and Cars drop folders.</summary>
        public List<GameObject> droppedCharacters = new List<GameObject>();
        public List<GameObject> droppedCars = new List<GameObject>();
        /// <summary>The animations inside each character model.</summary>
        public List<ModelClips> animations = new List<ModelClips>();
    }
}
