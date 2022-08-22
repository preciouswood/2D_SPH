using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Real
{
    public class SPH_Mono : MonoBehaviour
    {
        SPH_System system;

        [Header("粒子预制体")]
        public GameObject particlePrefab;
        [Header("是否自动运行")]
        public bool autoSimulate = true;
        [Header("粒子缩放")]
        [Range(1f,30f)]
        public float scale = 25f;
        [Header("模拟迭代次数")]
        [Range(1, 5)]
        public int iteraTimes = 3;
        [Header("粒子颜色绘制模式")]
        public DrawMode mode = DrawMode.DependOnPressure;
        [Header("粒子数量")]
        [Range(10, 1000)]
        public int particelCount = 500;
        [Header("Stiff")]
        [Range(1000, 2000)]
        public float stiff = 1500;
        [Header("StiffN")]
        [Range(2000, 3000)]
        public float stiffN = 2500;
        [Header("Rest Density")]
        [Range(0.1f, 0.5f)]
        public float restDensity = 0.2f;
        [Header("边界")]
        public Vector2 boundary = new Vector2(3000f,1500f);
        [HideInInspector]
        public Vector2 greavityDirection = Vector2.down;

        int realParicleCount;
        Transform[] particleTransArr;
        SpriteRenderer[] particleSpriteArr;
        void Start()
        {
            realParicleCount = particelCount;

            system = new SPH_System();
            system.Init(realParicleCount, 35f, stiff, stiffN, restDensity, boundary);
            system.OnUpdate(1);

            particleTransArr = new Transform[realParicleCount];
            particleSpriteArr = new SpriteRenderer[realParicleCount];
            AddParticleInstance();
        }

        void Update()
        {
            if(autoSimulate)
            {
                system.UpdateProp(stiff, stiffN, restDensity, Vector2.ClampMagnitude(greavityDirection, 1f));
                system.OnDrag(Input.GetMouseButton(0), Camera.main.ScreenToWorldPoint(Input.mousePosition));
                system.OnUpdate(iteraTimes);
            }
            SetupParticlePosAndColor();
        }

        void AddParticleInstance()
        {
            for (int i = 0; i < realParicleCount; i++)
            {
                system.GetParticlePosAndColor(i, out Vector2 pos, out Color col, mode);
                var instance = GameObject.Instantiate(particlePrefab, pos, Quaternion.identity);
                particleTransArr[i] = instance.transform;
                particleSpriteArr[i] = instance.GetComponent<SpriteRenderer>();
            }
        }
        void SetupParticlePosAndColor()
        {
            for (int i = 0; i < realParicleCount; i++)
            {
                system.GetParticlePosAndColor(i, out Vector2 pos, out Color col, mode);
                particleTransArr[i].localScale = Vector3.one * scale;
                particleTransArr[i].position = pos;
                particleSpriteArr[i].color = col;
            }
        }
    }
}