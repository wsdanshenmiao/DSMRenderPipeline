using System;
using UnityEngine;

public class TestSSR : MonoBehaviour
{
    struct Ray
    {
        public Vector3 origin;
        public Vector3 rayDir;
    }

    private Camera m_Camera;
    
    public float _RayMarchingStep = 0.1f;
    public float _RayMarchingMaxDistance = 100;

    private Matrix4x4 UNITY_MATRIX_P;
    
    private void Awake()
    {
        m_Camera = GetComponent<Camera>();
        UNITY_MATRIX_P = m_Camera.projectionMatrix;
    }
    
    // Update is called once per frame
    void Update()
    {
        Ray ray = new Ray();

        int marchingCount = (int)(_RayMarchingMaxDistance / _RayMarchingStep);
        // 限制到近平面内
        float rayLen = (ray.origin.z + ray.rayDir.z * _RayMarchingMaxDistance) < GetNearPlane()
            ? (ray.origin.z - GetNearPlane()) / ray.rayDir.z
            : _RayMarchingMaxDistance;
        Vector3 endPosVS = ray.origin + ray.rayDir * rayLen;

        // 转换到NDC空间 [-1, 1]
        /*Vector4 startCS = mul(UNITY_MATRIX_P, new Vector4(ray.origin, 1));
        Vector4 endPosCS = mul(UNITY_MATRIX_P, new Vector4(endPosVS, 1));
        float startK = 1.0f / startCS.w, endK = 1.0f / endPosCS.w;
        startCS *= startK;
        endPosCS *= endK;

        // 变换到屏幕空间
        Vector2 widthHeight = new Vector2(GetCameraTexWidth(), GetCameraTexHeight());
        Vector2 invWH = 1.0f / widthHeight;
        Vector2 startSS = (startCS.xy * 0.5 + 0.5) * widthHeight;
        Vector2 endSS = (endPosCS.xy * 0.5 + 0.5) * widthHeight;
        // 由于后续需要得知当前点的深度，因此还需要保存视图空间下的坐标
        // 由于屏幕空间的步进和视图空间的步进不是线性关系，因此需要使用齐次坐标下的 W 来进行联系
        Vector3 startQ = ray.origin * startK;
        Vector3 endQ = endPosVS * endK;

        bool steep = false; // 斜率是否大于1
        Vector2 offsetSS = endSS - startSS;
        if (abs(offsetSS.y) > abs(offsetSS.x)) {
            // 若斜率大于1则互换
            offsetSS = offsetSS.yx;
            startSS = startSS.yx;
            endSS = endSS.yx;
            steep = true;
        }

        // 步进的方向
        float stepDir = sign(offsetSS.x), invDx = stepDir / offsetSS.x;
        // 每次步进各个变量的偏移
        Vector3 offsetQ = (endQ - startQ) * invDx;
        float offsetK = (endK - startK) * invDx;
        offsetSS = new Vector2(stepDir, offsetSS.y * invDx);

        Vector2 currPos = startSS;
        Vector3 currQ = startQ;
        float currK = startK;
        float preZ = ray.origin.z;
        for (int iii = 0;
             (currPos.x * stepDir < endSS.x * stepDir) && iii < marchingCount;
             ++iii, currPos += offsetSS, currQ.z += offsetQ.z, currK += offsetK) {
            Vector2 uv = currPos * invWH;
            uv = steep ? uv.yx : uv.xy;
            // 通过 K 这个中介项复原深度
            float sceneZ = GetCameraLinearDepth(uv);

            float minZ = preZ;
            float maxZ = (currQ.z + 0.5f * offsetQ.z) / (currK + 0.5f * offsetK);
            preZ = maxZ;
            if (minZ > maxZ) {
                float tmp = maxZ;
                maxZ = minZ;
                minZ = tmp;
            }

            if (minZ <= sceneZ && maxZ > sceneZ - _HitThreshold) {
                return GetCameraColor(uv);
            }
        }*/
    }

    private Vector4 mul(Matrix4x4 m, Vector4 v)
    {
        return m.MultiplyVector(v);
    }

    private float abs(float v)
    {
        return Math.Abs(v);
    }
    
    private float GetNearPlane()
    {
        return m_Camera.nearClipPlane;
    }
    
}
