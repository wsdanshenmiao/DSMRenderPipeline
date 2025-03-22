using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSM
{
    [System.Serializable]

    public class ShadowSetting
    {
        // ShadowMap 的大小
        public enum MapSize
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }

        [Serializable]
        public struct Directional
        {
            public MapSize atlasSize;
        }

        [Min(0)] public float maxDistance = 100;
        public Directional directional = new Directional { atlasSize = MapSize._1024 };

    }
}
