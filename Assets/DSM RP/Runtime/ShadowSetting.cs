using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
            public MapSize m_AtlasSize;
            [Range(0, 4)] public int m_CascadeCount;
            [Range(0, 1)] public float m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3;
            public Vector3 m_CascadeRatios =>
                new Vector3(m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3);
        }

        [Min(0)] public float m_MaxDistance = 100;
        public Directional m_Directional = new Directional
        {
            m_AtlasSize = MapSize._1024,
            m_CascadeCount = 4,
            m_CascadeRatio1 = 0.1f,
            m_CascadeRatio2 = 0.25f,
            m_CascadeRatio3 = 0.5f,
        };

    }
}
