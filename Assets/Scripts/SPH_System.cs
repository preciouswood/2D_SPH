using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Assets.Scripts.Real
{
    public enum DrawMode
    {
        None,
        DependOnPressure,//根据粒子压力着色
        DependOnVelocity,//根据粒子方向着色
    }
    class SPH_System
    {
        public const float pull = .2f;
        public const float smooth = 35;

        #region 粒子相关

        class Particle
        {

            public Vector2 pos;
            public Vector2 oldPos;
            public Vector2 velocity;
            public float dens, densN;
            public float press, pressN;
            public bool grabbed;

            public Particle(Vector2 pos)
            {
                this.pos = pos;
                this.oldPos = pos;
                this.velocity = Vector2.zero;
                dens = 0;
                densN = 0;
                press = 0;
                pressN = 0;
                grabbed = false;
            }
        }

        #endregion

        #region 粒子连接对象相关

        /// <summary>
        /// 粒子连接对象
        /// </summary>
        class Pair : IPoolObj
        {
            public Particle a, b;
            public float q, q2, q3;
            public Pair()
            {
                q = q2 = q3 = 0;
            }
            /// <summary>
            /// 设置连接对象中的两个粒子
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            public void Setup(Particle x, Particle y)
            {
                a = x;
                b = y;
            }
            
            public void Reset()
            {
                a = b = null;
                q = q2 = q3 = 0;
            }
        }
        #endregion

        /// <summary>
        /// 流体控制器
        /// </summary>
        class SphFluid
        {
            const float pointsize = 15;
            const float gravity = 9.8f;

            readonly IList<Particle> particles;
            readonly IList<Pair> pairs;
            readonly int particleCount;
            float particleRadius, kstiff, kstiffN, krestDensity;
            float reach;

            bool enableMouseDrag = false;
            Vector2 mousePos;
            Vector2 gravityDir;
            Vector2 boundary;

            readonly ParticleContainer container;
            readonly ObjPool<Pair> objPool;
            public SphFluid(int num, float ksm, float kst, float kstn, float kr, float reach, Vector2 boundary)
            {
                this.particleCount = num;
                particleRadius = ksm;
                kstiff = kst;
                kstiffN = kstn;
                krestDensity = kr;
                this.reach = reach;
                this.boundary = boundary;

                particles = new List<Particle>(num);
                pairs = new List<Pair>(num * 12);
                container = new ParticleContainer(ksm, boundary);
                objPool = new ObjPool<Pair>();

                float interval = pointsize * 1.5f;
                float initx = interval;
                float inity = interval;
                float row = 0;
                for (int i = 0; i < num; i++)
                {
                    particles.Add(new Particle(new Vector2(initx + row * 5, inity)));
                    initx += interval;
                    if (initx > boundary.x - interval)
                    {
                        initx = interval;
                        inity += interval;
                        row++;
                    }
                }
            
            }

            /// <summary>
            /// 抓取粒子
            /// </summary>
            /// <param name="mousePos"></param>
            void Drag(Vector2 mousePos)
            {
                var neighbourParticleIndex = container.GetNeighbourParticleIndices(mousePos);
                foreach (var index in neighbourParticleIndex)
                {
                    Particle p = particles[index];
                    float dist = (p.pos - mousePos).magnitude;
                    if (dist < reach)
                    {
                        p.grabbed = true;
                    }
                }
            }
            /// <summary>
            /// 释放抓取的粒子
            /// </summary>
            void LetGo()
            {
                for (int i = 0; i < particleCount; i++)
                {
                    Particle p = particles[i];
                    p.grabbed = false;
                }
            }

            /// <summary>
            /// 设置属性
            /// </summary>
            /// <param name="kst"></param>
            /// <param name="kstn"></param>
            /// <param name="kr"></param>
            /// <param name="gravityDir"></param>
            public void SetupProp(float kst, float kstn, float kr, Vector2 gravityDir)
            {
                kstiff = kst;
                kstiffN = kstn;
                krestDensity = kr;
                this.gravityDir = gravityDir;
            }

            /// <summary>
            /// 拖拽粒子
            /// </summary>
            /// <param name="enableMouseDrag"></param>
            /// <param name="mousePos"></param>
            public void SetupDrag(bool enableMouseDrag, Vector2 mousePos)
            {
                this.enableMouseDrag = enableMouseDrag;
                this.mousePos = mousePos;
                if (enableMouseDrag)
                    Drag(mousePos);
                else
                    LetGo();
            }

            /// <summary>
            /// 模拟仿真
            /// </summary>
            /// <param name="deltaTime"></param>
            public void Simulate(float deltaTime)
            {
                for (int i = 0; i < pairs.Count; i++)
                    objPool.Return(pairs[i]);
                pairs.Clear();

                #region 处理 重力 边界 惯性

                for (int i = 0; i < particleCount; i++)
                {
                    Particle p = particles[i];
                    p.velocity = (p.pos - p.oldPos) / deltaTime;

                    // gravity
                    p.velocity += gravityDir * gravity;

                    // grab
                    if (enableMouseDrag && p.grabbed)
                    {
                        var v = mousePos - p.pos;
                        p.velocity += pull * v;
                    }

                    // bounds
                    if (p.pos.x <= pointsize / 2 || p.pos.x >= boundary.x - pointsize / 2)
                    {
                        if (p.pos.x <= pointsize / 2)
                        {
                            p.pos.x = pointsize / 2 * 1.05f;
                        }
                        else
                        {
                            p.pos.x = boundary.x - pointsize / 2 * 1.05f;
                        }
                        p.velocity.x *= -.3f;
                    }
                    if (p.pos.y >= boundary.y - pointsize / 2 || p.pos.y <= pointsize / 2)
                    {
                        if (p.pos.y >= boundary.y - pointsize / 2)
                        {
                            p.pos.y = boundary.y - pointsize / 2 * 1.05f;
                        }
                        else
                        {
                            p.pos.y = pointsize / 2 * 1.05f;
                        }
                        p.velocity.y *= -.2f;
                    }


                    p.oldPos = p.pos;
                    p.pos += p.velocity * deltaTime;
                    p.dens = 0;
                    p.densN = 0;
                }

                #endregion

                #region 处理邻居粒子影响

                container.SetupNeighbourIndex(particles);
                for (int i = 0; i < particleCount; i++)
                {
                    Particle p1 = particles[i];
                    var neighbourIndices = container.GetNeighbourParticleIndices(p1.pos);
                    for (int j = 0; j < neighbourIndices.Count; j++)
                    {
                        var index = neighbourIndices[j];
                        if (i != index)
                        {
                            Particle p2 = particles[index];
                            float dist = (p1.pos - p2.pos).sqrMagnitude;
                            if (dist < particleRadius * particleRadius)
                            {
                                var pair = objPool.Get();
                                pair.Setup(p1, p2);
                                pairs.Add(pair);
                            }
                        }
                    }
                }


                for (int i = 0; i < pairs.Count; i++)
                {
                    Pair p = pairs[i];
                    float dist = (p.a.pos - p.b.pos).magnitude;
                    p.q = 1 - dist / particleRadius;
                    p.q2 = Mathf.Pow(p.q, 2);
                    p.q3 = Mathf.Pow(p.q, 3);
                    p.a.dens += p.q2;
                    p.b.dens += p.q2;
                    p.a.densN += p.q3;
                    p.b.densN += p.q3;
                }

                for (int i = 0; i < particleCount; i++)
                {
                    Particle p = particles[i];
                    p.press = kstiff * (p.dens - krestDensity);
                    p.pressN = kstiffN * p.densN;
                    if (p.press > 6000)
                    {
                        p.press = 6000;
                    }
                    if (p.pressN > 7000)
                    {
                        p.pressN = 7000;
                    }
                }

                for (int i = 0; i < pairs.Count; i++)
                {
                    Pair p = pairs[i];
                    float press = p.a.press + p.b.press;
                    float pressN = p.a.pressN + p.b.pressN;
                    float displace = (press * p.q + pressN * p.q2) * Mathf.Pow(deltaTime, 2);
                    float dist = (p.a.pos - p.b.pos).magnitude;
                    var ab = (p.a.pos - p.b.pos) / dist;
                    p.a.pos += displace * ab;
                    p.b.pos -= displace * ab;
                }


                #endregion
            }

            #region 设置粒子坐标与颜色相关

            const float pressred = 5000;
            const float pressblue = 200;
            const float MaxVelocityInverse = 0.01f;
            /// <summary>
            /// 根据粒子索引 获取 位置 与 颜色
            /// </summary>
            /// <param name="particleIndex"></param>
            /// <param name="pos"></param>
            /// <param name="col"></param>
            /// <param name="drawMode"></param>
            public void GetParticlePosAndColor(int particleIndex, out Vector2 pos, out Color col, DrawMode drawMode)
            {
                var p = particles[particleIndex];
                pos = p.pos;

                switch (drawMode)
                {
                    case DrawMode.DependOnPressure:
                        var press = p.press;
                        float red = 0.2f;
                        float green = 0.2f;
                        float blue = 1f;
                        float diff = pressred - pressblue;
                        float quad = diff / 4;
                        if (press < pressblue)
                        {
                        }
                        else if (press < pressblue + quad)
                        {
                            float segdiff = press - pressblue;
                            green = segdiff / quad;
                        }
                        else if (press < pressblue + 2 * quad)
                        {
                            float segdiff = press - pressblue - quad;
                            green = 1f;
                            blue = 1f - segdiff / quad;
                        }
                        else if (press < pressblue + 3 * quad)
                        {
                            float segdiff = press - 200 - 2 * quad;
                            green = 1f;
                            blue = 0.5f;
                            red = segdiff / quad;
                        }
                        else if (press < pressblue)
                        {
                            float segdiff = press - pressblue - 3 * quad;
                            red = 1f;
                            blue = 0.2f;
                            green = 1f - segdiff / quad;
                        }
                        else
                        {
                            red = 1f;
                            blue = 0.2f;
                        }
                        col = new Color(red, green, blue, 1f);
                        break;
                    case DrawMode.DependOnVelocity:
                        var vel = p.velocity.magnitude;

                        var dir = p.velocity.normalized + Vector2.one;
                        var dynamicCol = new Color(dir.x * .5f * vel * MaxVelocityInverse, dir.y * .5f * vel * MaxVelocityInverse, 0f, 1f);
                        col = dynamicCol;
                        break;

                    case DrawMode.None:
                    default:
                        col = Color.white;
                        break;
                }
            }

            #endregion
        }

        #region 粒子容器相关
        /// <summary>
        /// 粒子容器 提供索引邻居粒子的方法
        /// </summary>
        class ParticleContainer
        {
            readonly float cellSize;
            readonly Vector2Int cellCount;
            readonly Vector2 containerSize;
            readonly List<int> currentNeighbourParticleIndex;
            readonly List<int>[,] particleIndex;
            public ParticleContainer(float cellSize, Vector2 containerSize)
            {
                this.cellSize = cellSize;
                this.containerSize = containerSize;

                cellCount = new Vector2Int(Mathf.CeilToInt(containerSize.x / cellSize), Mathf.CeilToInt(containerSize.y / cellSize));

                currentNeighbourParticleIndex = new List<int>(64);
                particleIndex = new List<int>[cellCount.x, cellCount.y];

                for (int i = 0; i < cellCount.x; i++)
                {
                    for (int j = 0; j < cellCount.y; j++)
                    {
                        particleIndex[i, j] = new List<int>(12);
                    }
                }
            }

            /// <summary>
            /// 设置所有cell的粒子索引
            /// </summary>
            /// <param name="particles"></param>
            public void SetupNeighbourIndex(IList<Particle> particles)
            {
                for (int i = 0; i < cellCount.x; i++)
                {
                    for (int j = 0; j < cellCount.y; j++)
                    {
                        particleIndex[i, j].Clear();
                    }
                }

                for (int i = 0; i < particles.Count; i++)
                {
                    var index = GetCellIndex(particles[i].pos);
                    if (index.x > -1 && index.x < cellCount.x && index.y > -1 && index.y < cellCount.y)
                        particleIndex[index.x, index.y].Add(i);
                }
            }

            #region 获取粒子邻居粒子相关
            /// <summary>
            /// 周围九个cell的索引偏移数组
            /// </summary>
            readonly static Vector2Int[] NeighbourIndex = new Vector2Int[]
            {
                new Vector2Int( 0,  0),
                new Vector2Int(-1,  0), new Vector2Int(1,  0),
                new Vector2Int( 0, -1), new Vector2Int(0,  1),
                new Vector2Int(-1, -1), new Vector2Int(1,  1),
                new Vector2Int(-1,  1), new Vector2Int(1, -1),
            };
            /// <summary>
            /// 根据粒子位置获取半径范围内的所有粒子索引列表
            /// </summary>
            /// <param name="pos"></param>
            /// <returns></returns>
            public IList<int>/*void*/ GetNeighbourParticleIndices(Vector2 pos)
            {
                currentNeighbourParticleIndex.Clear();
                var index = GetCellIndex(pos);

                for (int i = 0; i < NeighbourIndex.Length; i++)
                {
                    var list = GetParticleIndex(index + NeighbourIndex[i]);
                    if(list != null)
                    {
                        for (int j = 0; j < list.Count; j++)
                        {
                            currentNeighbourParticleIndex.Add(list[j]);
                            //yield return list[j];
                        }
                    }
                }
                return currentNeighbourParticleIndex;
            }
            #endregion

            /// <summary>
            /// 根据位置获取cell索引
            /// </summary>
            /// <param name="pos"></param>
            /// <returns></returns>
            Vector2Int GetCellIndex(Vector2 pos)
            {
                return new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.y / cellSize));
            }
            /// <summary>
            /// 根据cell索引获取对应cell中包含的粒子索引列表
            /// </summary>
            /// <param name="cellIndex"></param>
            /// <returns></returns>
            IList<int> GetParticleIndex(Vector2Int cellIndex)
            {
                if (cellIndex.x > -1 && cellIndex.x < cellCount.x && cellIndex.y > -1 && cellIndex.y < cellCount.y)
                    return particleIndex[cellIndex.x, cellIndex.y];
                return null;
            }
        }
        #endregion

        #region 对象池相关
        /// <summary>
        /// 回收类接口
        /// </summary>
        interface IPoolObj
        {
            /// <summary>
            /// 回收时调用
            /// </summary>
            void Reset();
        }
        /// <summary>
        /// 建议对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        class ObjPool<T> where T : IPoolObj , new()
        {
            readonly Stack<T> stack;
            public ObjPool()
            {
                stack = new Stack<T>();
            }
            /// <summary>
            /// 获取一个对象
            /// </summary>
            /// <returns></returns>
            public T Get()
            {
                if (stack.Count > 0)
                    return stack.Pop();
                else
                    return new T();
            }
            /// <summary>
            /// 回收一个对象
            /// </summary>
            /// <param name="item"></param>
            public void Return(T item)
            {
                item.Reset();
                stack.Push(item);
            }
        }

        #endregion

        SphFluid fluid;
        public void Init(int particleCount, float particelRadius, float stiff, float stiffN, float restDensity, Vector2 boundary)
        {
            fluid = new SphFluid(particleCount, particelRadius, stiff, stiffN, restDensity, 100f, boundary);
        }

        public void UpdateProp(float kst, float kstn, float kr, Vector2 gravityDir)
        {
            fluid.SetupProp(kst, kstn, kr, gravityDir);
        }

        public void OnDrag(bool enableMouseDrag, Vector2 mousePos)
        {
            fluid.SetupDrag(enableMouseDrag, mousePos);
        }

        public void OnUpdate(int iteraTimes)
        {
            for (int i = 0; i < iteraTimes; i++)
            {
                fluid.Simulate(0.01f);
            }
        }

        public void GetParticlePosAndColor(int particleIndex, out Vector2 pos, out Color col, DrawMode mode)
        {
            fluid.GetParticlePosAndColor(particleIndex, out pos, out col,mode);
        }

    }
}
