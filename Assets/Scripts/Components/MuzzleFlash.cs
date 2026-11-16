using UnityEngine;

/// <summary>
/// 枪口闪光（教授第4条：开火时打出黄色光效）
/// 每次开火在枪口生成一次：一盏短暂的黄色点光源 + 一个自发光小球，
/// 0.09 秒内亮度衰减到 0 后自动销毁。自发光小球配合场景 Bloom 会产生明显的光晕。
///
/// 说明：不依赖任何美术资源，材质是运行时用 URP Lit 现场创建的；
/// 若目标平台上找不到 shader，会自动退化为"只有一盏黄色点光"，不会报错。
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    Light flashLight;
    Transform glowPivot;
    Material glowMaterial;

    float duration = 0.09f;
    float peakLightIntensity = 6f;
    float peakGlowScale = 0.22f;
    float timer;

    /// <summary>在指定枪口生成一次闪光</summary>
    public static void Spawn(Transform muzzle, Color color, float lightIntensity, float duration)
    {
        GameObject go = new GameObject("MuzzleFlash");
        go.transform.SetParent(muzzle, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        MuzzleFlash flash = go.AddComponent<MuzzleFlash>();
        flash.Init(color, lightIntensity, duration);
    }

    void Init(Color color, float lightIntensity, float duration)
    {
        this.duration = Mathf.Max(0.02f, duration);
        this.peakLightIntensity = Mathf.Max(0f, lightIntensity);

        // 1) 点光源：让开火瞬间照亮枪口、地面和附近敌人
        GameObject lightGo = new GameObject("FlashLight");
        lightGo.transform.SetParent(transform, false);
        flashLight = lightGo.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.86f, 0.35f);
        flashLight.range = 5f;
        flashLight.intensity = peakLightIntensity;
        flashLight.shadows = LightShadows.None;

        // 2) 自发光小球：Bloom 会把它糊成一片黄色光晕
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Glow";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * peakGlowScale;
        glowPivot = sphere.transform;

        Collider primitiveCollider = sphere.GetComponent<Collider>();
        if (primitiveCollider != null) Destroy(primitiveCollider);

        glowMaterial = CreateGlowMaterial(color);
        if (glowMaterial != null)
        {
            MeshRenderer mr = sphere.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = glowMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
        else
        {
            // 没有可用 shader 时，退化成只有光
            Destroy(sphere);
        }
    }

    /// <summary>用 URP Lit 现场造一个高强度自发光的材质</summary>
    Material CreateGlowMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.name = "MuzzleFlashGlow";

        Color baseColor = new Color(1f, 0.9f, 0.5f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        mat.color = baseColor;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 8f); // 乘 8 保证超过 Bloom 的 0.9 阈值
        }
        return mat;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        if (flashLight != null)
            flashLight.intensity = Mathf.Lerp(peakLightIntensity, 0f, t);

        if (glowPivot != null)
            glowPivot.localScale = Vector3.one * Mathf.Lerp(peakGlowScale, peakGlowScale * 1.6f, t);

        if (timer >= duration)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
