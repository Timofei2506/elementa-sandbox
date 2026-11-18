using System;
using System.Collections.Generic;
using System.Linq;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Инструменты песочницы (как в Melon Sandbox): никакого «персонажа» — игрок управляет
    /// мышью/пальцем и сыплет в мир объекты, оружие, жидкости, огонь и НПС.
    /// Ввод общий для мыши и тача.
    /// </summary>
    public enum ToolId
    {
        Select,        // таскать и бросать объекты
        Spawn,         // инвентарь: материал + форма + размер
        Fluid,         // налить вещество / наполнить ёмкость
        Weapon,        // оружие из инвентаря (стрелковое, ближний бой, метательное)
        Fire,          // поджечь
        Heat,          // горелка / криолуч
        Hammer,        // удар
        Npc,           // создать персонажа-рагдолла
        Delete,        // удалить
        Grab,          // притянуть/оттолкнуть (гравитационная пушка)
    }

    public class PlayerController : MonoBehaviour
    {
        public ToolId CurrentTool { get; private set; } = ToolId.Select;
        public MaterialDef SelectedMaterial { get; private set; }
        public SubstanceDef SelectedSubstance { get; private set; }
        public BodyShape SelectedShape { get; private set; } = BodyShape.Box;
        public NpcActor.NpcKind SelectedNpcKind { get; private set; } = NpcActor.NpcKind.Human;
        public WeaponSystem Weapons { get; private set; }
        public SandboxObject Selected { get; private set; }
        public FluidContainer SelectedContainer { get; private set; }
        public NpcActor SelectedNpc { get; private set; }
        public string SelectionDescription { get; private set; } = "Ничего не выбрано — наведите курсор";
        public float HeatPower = 700f;
        public float SpawnScale = 1f;

        /// <summary>Последняя точка в плоскости экрана — её использует UI для предпросмотра.</summary>
        public Vector3 PointerWorld { get; private set; }

        SandboxWorld _world;
        CameraRig _rig;
        Rigidbody _dragged;
        Vector3 _dragOffset;
        float _lastUse;
        bool _pointerDown;
        Vector3 _pointerPos;
        float _nextSpawn;

        public static PlayerController Create(SandboxWorld world)
        {
            var go = new GameObject("PlayerController");
            go.transform.SetParent(world.transform, false);
            var pc = go.AddComponent<PlayerController>();
            pc._world = world;
            pc.Weapons = WeaponSystem.Create(go.transform);
            pc._rig = Game.I.CameraRig;
            pc.SelectedMaterial = Game.I.Materials.TryGet("steel_1045") ?? Game.I.Materials.All.FirstOrDefault();
            pc.SelectedSubstance = Game.I.Substances.TryGet("water") ?? Game.I.Substances.All.FirstOrDefault();
            pc.Weapons.Select("pistol_9mm");
            return pc;
        }

        public void SelectTool(ToolId tool)
        {
            CurrentTool = tool;
            _dragged = null;
            if (tool == ToolId.Weapon && Weapons.Current == null) Weapons.Select("pistol_9mm");
            Game.I.UI?.Log("Инструмент: " + ToolName(tool));
        }

        public static string ToolName(ToolId t) => t switch
        {
            ToolId.Select => "Рука",
            ToolId.Spawn => "Инвентарь",
            ToolId.Fluid => "Жидкость",
            ToolId.Weapon => "Оружие",
            ToolId.Fire => "Огонь",
            ToolId.Heat => "Горелка",
            ToolId.Hammer => "Молот",
            ToolId.Npc => "Персонаж",
            ToolId.Delete => "Ластик",
            ToolId.Grab => "Гравипушка",
            _ => t.ToString()
        };

        public static string ShortToolName(ToolId t) => t switch
        {
            ToolId.Select => "Рука",
            ToolId.Spawn => "Предмет",
            ToolId.Fluid => "Жидк.",
            ToolId.Weapon => "Оружие",
            ToolId.Fire => "Огонь",
            ToolId.Heat => "Жар",
            ToolId.Hammer => "Молот",
            ToolId.Npc => "НПС",
            ToolId.Delete => "Ластик",
            ToolId.Grab => "Грави",
            _ => t.ToString()
        };

        public static ToolId[] AllTools => new[]
        {
            ToolId.Select, ToolId.Spawn, ToolId.Fluid, ToolId.Weapon, ToolId.Fire,
            ToolId.Heat, ToolId.Hammer, ToolId.Npc, ToolId.Grab, ToolId.Delete
        };

        public void SetMaterial(MaterialDef mat) { SelectedMaterial = mat; CurrentTool = ToolId.Spawn; }
        public void SetSubstance(SubstanceDef s) { SelectedSubstance = s; CurrentTool = ToolId.Fluid; }
        public void SetShape(BodyShape shape) => SelectedShape = shape;
        public void SetNpcKind(NpcActor.NpcKind kind) => SelectedNpcKind = kind;
        public void SetWeapon(WeaponDef w) { Weapons.Select(w.id); CurrentTool = ToolId.Weapon; }

        void Update()
        {
            if (Game.I == null || _world == null) return;

            HandleKeyboardShortcuts();

            bool overUI = IsPointerOverUI();
            Game.I.UI?.SetPointerOverUI(overUI);

            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                _pointerPos = touch.position;
                if (touch.phase == TouchPhase.Began && !overUI)
                {
                    _pointerDown = true;
                    UseTool(_pointerPos, true);
                }
                else if (touch.phase == TouchPhase.Moved && _pointerDown && CurrentTool is ToolId.Heat or ToolId.Hammer or ToolId.Weapon or ToolId.Select or ToolId.Fluid or ToolId.Spawn or ToolId.Delete)
                {
                    UseTool(_pointerPos, false);
                }
                else if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                {
                    _pointerDown = false;
                    EndDrag();
                }
            }
            else
            {
                _pointerPos = Input.mousePosition;
                if (Input.GetMouseButtonDown(0) && !overUI)
                {
                    _pointerDown = true;
                    UseTool(_pointerPos, true);
                }
                else if (Input.GetMouseButton(0) && _pointerDown)
                {
                    UseTool(_pointerPos, false);
                }
                if (Input.GetMouseButtonUp(0)) { _pointerDown = false; EndDrag(); }
            }

            UpdateDrag();
            UpdateRaycastTarget(_pointerPos);
            UpdateHoverIndicator(_pointerPos);
        }

        static readonly List<UnityEngine.EventSystems.RaycastResult> _uiHits = new List<UnityEngine.EventSystems.RaycastResult>();
        static bool IsPointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es)
            {
                position = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition
            };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ToolId.Select);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ToolId.Spawn);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ToolId.Fluid);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(ToolId.Weapon);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTool(ToolId.Fire);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectTool(ToolId.Heat);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectTool(ToolId.Hammer);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SelectTool(ToolId.Npc);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SelectTool(ToolId.Grab);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SelectTool(ToolId.Delete);
            if (Input.GetKeyDown(KeyCode.F1)) Game.I.UI?.ShowHelp();
            if (Input.GetKeyDown(KeyCode.Space)) _world.TimeScale = _world.TimeScale > 0.01f ? 0f : 1f;
            if (Input.GetKeyDown(KeyCode.Z)) CycleWeapon(-1);
            if (Input.GetKeyDown(KeyCode.X)) CycleWeapon(1);
            if (Input.GetKeyDown(KeyCode.R)) _world.ResetWorld();
            if (Input.GetKeyDown(KeyCode.Delete) && Selected != null) { Destroy(Selected.gameObject); Selected = null; }
            if (Input.GetKeyDown(KeyCode.Return)) Weapons.Refill();
            if (Input.GetKeyDown(KeyCode.Tab)) Game.I.UI?.ToggleCatalog();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SpawnScale = Mathf.Max(0.25f, SpawnScale - 0.15f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SpawnScale = Mathf.Min(4f, SpawnScale + 0.15f);
        }

        public void CycleWeapon(int direction)
        {
            var all = Game.I.Weapons.All;
            if (all.Count == 0) return;
            int idx = Weapons.Current != null ? all.ToList().FindIndex(x => x.id == Weapons.Current.id) : 0;
            idx = ((idx + direction) % all.Count + all.Count) % all.Count;
            SetWeapon(all[idx]);
            Game.I.UI?.Log("Оружие: " + all[idx].nameRu);
        }

        /// <summary>Точка в мире под курсором (плоскость z = 0).</summary>
        public bool TryPointerWorld(Vector3 screenPos, out Vector3 world) => _rig.ScreenToWorld(screenPos, out world);

        void UseTool(Vector3 screenPos, bool firstPress)
        {
            if (!_rig.ScreenToWorld(screenPos, out var point)) return;
            PointerWorld = point;

            switch (CurrentTool)
            {
                case ToolId.Select: HandleSelect(point, firstPress); break;
                case ToolId.Spawn: SpawnAt(point, firstPress); break;
                case ToolId.Fluid: PourFluid(point); break;
                case ToolId.Weapon: UseWeapon(point, firstPress); break;
                case ToolId.Fire: if (firstPress) IgniteAt(point); break;
                case ToolId.Heat: ApplyHeat(point, firstPress); break;
                case ToolId.Hammer: if (firstPress || Time.time - _lastUse > 0.22f) HammerAt(point); break;
                case ToolId.Npc: if (firstPress) SpawnNpc(point); break;
                case ToolId.Delete: DeleteAt(point); break;
                case ToolId.Grab: GrabAt(point, firstPress); break;
            }
            _lastUse = Time.time;
        }

        // ---------------- helpers ----------------
        SandboxObject ObjectAt(Vector3 point, float radius = 0.4f) => _world.ObjectAt(point, radius);
        NpcActor NpcAt(Vector3 point, float radius = 0.6f) => _world.NpcAt(point, radius);
        FluidContainer ContainerAt(Vector3 point, float radius = 0.7f) => _world.ContainerAt(point, radius);

        // ---------------- tools ----------------
        void HandleSelect(Vector3 point, bool firstPress)
        {
            if (!firstPress) return;
            var obj = ObjectAt(point, 0.6f);
            var npc = NpcAt(point, 0.8f);
            SelectedNpc = npc;
            SelectedContainer = ContainerAt(point);
            Selected = obj;
            if (obj != null)
            {
                _dragged = obj.Body;
                _dragOffset = obj.transform.position - point;
                _dragOffset.z = 0f;
            }
        }

        void EndDrag() => _dragged = null;

        void UpdateDrag()
        {
            if (_dragged == null || !_pointerDown) return;
            if (!_rig.ScreenToWorld(_pointerPos, out var point)) return;
            var target = point + _dragOffset;
            var current = _dragged.position;
            var vel = (target - current) * 12f;
            vel.z = 0f;
            PhysicsCompat.SetVelocity(_dragged, Vector3.ClampMagnitude(vel, 26f));
        }

        void SpawnAt(Vector3 point, bool firstPress)
        {
            if (SelectedMaterial == null) return;
            // зажатая ЛКМ сыплет предметы — как «песок» в Melon Sandbox
            if (!firstPress && Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + 0.09f;

            const float limit = WorldEnvironment.ArenaHalfSize - 1f;
            point.x = Mathf.Clamp(point.x, -limit, limit);
            point.y = Mathf.Clamp(point.y, 0.3f, 22f);
            Vector3 size = ShapeSize(SelectedShape) * SpawnScale;
            var obj = _world.Spawn(SelectedMaterial, SelectedShape, size, point, UnityEngine.Random.Range(0f, 360f));
            if (obj != null && firstPress) Game.I.UI?.Log($"Создан объект: {SelectedMaterial.nameRu}");
        }

        public static Vector3 ShapeSize(BodyShape shape) => shape switch
        {
            BodyShape.Plate => new Vector3(1.8f, 0.28f, 0.3f),
            BodyShape.Sphere => Vector3.one * 1.05f,
            BodyShape.Cylinder => new Vector3(0.9f, 1.3f, 0.3f),
            BodyShape.Capsule => new Vector3(0.75f, 1.15f, 0.3f),
            BodyShape.Ragdoll => new Vector3(0.8f, 1.6f, 0.3f),
            _ => Vector3.one,
        };

        void PourFluid(Vector3 point)
        {
            if (SelectedSubstance == null) return;
            if (Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + 0.05f;

            var container = ContainerAt(point);
            if (container != null)
            {
                container.Add(SelectedSubstance.id, 250f);
                return;
            }
            var c = _world.SpawnContainer(new Vector3(point.x, point.y + 0.6f, 0f), 8f);
            c.Fill(SelectedSubstance.id, 1.2f);
            Game.I.UI?.Log($"Налито: {SelectedSubstance.nameRu}");
        }

        void UseWeapon(Vector3 point, bool firstPress)
        {
            var w = Weapons.Current;
            if (w == null) { Weapons.Select("pistol_9mm"); return; }

            if (w.IsMelee)
            {
                if (!firstPress && Time.time - _lastUse < 60f / Mathf.Max(30f, w.rpm)) return;
                MeleeStrike(point, w);
                return;
            }

            if (!firstPress && !w.automatic) return;
            if (!Weapons.CanFire)
            {
                if (Weapons.Ammo <= 0) Weapons.Refill();
                return;
            }
            var origin = new Vector3(point.x, point.y, 0f) - new Vector3(0f, 0f, 1f);
            var dir = Vector3.forward;
            // метательное оружие бросаем из точки «руки» вниз-вперёд, чтобы летело по дуге
            if (w.IsThrown)
            {
                origin = new Vector3(point.x, point.y + 0.4f, -2f);
                dir = new Vector3(0f, -0.05f, 1f).normalized;
            }
            Weapons.Fire(origin, dir);
            AudioSystem.I?.Play(w.caliberMM > 20f ? "cannon" : "shot", w.caliberMM > 20f ? 0.9f : 0.5f, UnityEngine.Random.Range(0.94f, 1.06f));
        }

        void MeleeStrike(Vector3 point, WeaponDef w)
        {
            float radius = Mathf.Clamp(w.caliberMM * 0.006f + 0.45f, 0.5f, 2.2f);
            float impulse = Mathf.Clamp(w.massGrams * 0.06f + w.velocity * 0.5f, 4f, 90f);
            bool hitSomething = false;

            foreach (var obj in _world.Objects.ToArray())
            {
                if (obj == null) continue;
                if (Vector3.Distance(obj.transform.position, point) > radius + obj.transform.localScale.magnitude * 0.5f) continue;
                hitSomething = true;
                float dmg = w.kind switch
                {
                    ProjectileKind.Blade => w.massGrams * 0.09f + 12f,
                    ProjectileKind.Pierce => w.massGrams * 0.07f + 16f,
                    _ => w.massGrams * 0.02f + 18f,
                };
                obj.ApplyDamage(dmg);
                if (obj.Body != null)
                {
                    var dir = (obj.transform.position - point).normalized + Vector3.up * 0.2f;
                    obj.Body.AddForce(dir * impulse, ForceMode.Impulse);
                }
                AudioSystem.I?.ImpactFor(obj.Material, 0.8f);
                EffectsSystem.SpawnSparks(point, Vector3.up, 6, 3f);
            }

            foreach (var npc in _world.Npcs.ToArray())
            {
                if (npc == null || npc.IsDead) continue;
                if (Vector3.Distance(npc.transform.position, point) > radius + 0.9f) continue;
                hitSomething = true;
                float dmg = w.kind switch
                {
                    ProjectileKind.Blade => w.massGrams * 0.10f + 22f,
                    ProjectileKind.Pierce => w.massGrams * 0.08f + 26f,
                    _ => w.massGrams * 0.03f + 16f,
                };
                npc.ApplyDamage(dmg, point);
            }

            if (hitSomething)
            {
                ScreenShake.Impact(Mathf.Clamp01(impulse / 60f));
                EffectsSystem.SpawnFluidSplash(point, new Color(0.55f, 0.08f, 0.08f), 8, 3f);
            }
        }

        void IgniteAt(Vector3 point)
        {
            var obj = ObjectAt(point, 0.6f);
            var npc = NpcAt(point, 0.8f);
            var container = ContainerAt(point);
            if (obj != null) { obj.Ignite(); Game.I.UI?.Log($"{obj.Material.nameRu} подожжён"); }
            else if (npc != null) { npc.Ignite(); Game.I.UI?.Log("Персонаж горит"); }
            else if (container != null) { container.Spark(); }
            else EffectsSystem.SpawnFire(new Vector3(point.x, point.y, 0f), Vector3.one * 0.5f, 0.7f, null);
        }

        void ApplyHeat(Vector3 point, bool firstPress)
        {
            if (!firstPress && Time.time - _lastUse < 0.04f) return;
            var obj = ObjectAt(point, 0.7f);
            var container = ContainerAt(point);
            float joules = HeatPower * (firstPress ? 1200f : 380f);
            if (obj != null)
            {
                obj.AddHeat(joules);
                EffectsSystem.SpawnSparks(point, Vector3.up, 3, 2f);
                if (obj.Temperature > 900f) obj.Ignite();
            }
            else if (container != null)
            {
                container.Heat(HeatPower * 0.4f * Time.deltaTime + 30f);
                container.Spark();
            }
            else EffectsSystem.SpawnSmoke(new Vector3(point.x, point.y, 0f), 0.5f, 0.6f, 0.4f);
        }

        void HammerAt(Vector3 point)
        {
            var obj = ObjectAt(point, 0.7f);
            var npc = NpcAt(point, 0.9f);
            if (obj != null)
            {
                float damage = 45f + obj.Material.hardness * 0.02f;
                obj.ApplyDamage(damage, 1100f);
                AudioSystem.I?.ImpactFor(obj.Material, 1f);
                if (obj.Body != null)
                    obj.Body.AddForceAtPosition(Vector3.down * 240f, point, ForceMode.Impulse);
                EffectsSystem.SpawnSparks(point, Vector3.up, 8, 4f);
                ScreenShake.Impact(1f);
            }
            else if (npc != null)
            {
                npc.ApplyDamage(32f, point);
                ScreenShake.Impact(0.8f);
            }
            else
            {
                EffectsSystem.SpawnSparks(point, Vector3.up, 4, 3f);
                ScreenShake.Impact(0.3f);
            }
        }

        void SpawnNpc(Vector3 point)
        {
            var npc = _world.SpawnNpc(new Vector3(point.x, point.y, 0f), SelectedNpcKind);
            npc.SetBehaviour(NpcActor.Behaviour.Wander);
            Game.I.UI?.Log("Создан персонаж: " + SelectedNpcKind);
        }

        void DeleteAt(Vector3 point)
        {
            var obj = ObjectAt(point, 0.7f);
            if (obj != null)
            {
                EffectsSystem.SpawnFluidSplash(obj.transform.position, obj.Material.color, 12, 2.5f);
                Destroy(obj.gameObject);
                return;
            }
            var npc = NpcAt(point, 0.9f);
            if (npc != null) { npc.Remove(); return; }
            var container = ContainerAt(point);
            if (container != null) container.BreakOpen();
        }

        void GrabAt(Vector3 point, bool firstPress)
        {
            var obj = ObjectAt(point, 1.2f);
            if (obj == null || obj.Body == null) return;
            var dir = (point - obj.transform.position);
            obj.Body.AddForce(new Vector3(dir.x, dir.y, 0f) * 60f, ForceMode.Impulse);
            if (firstPress) Game.I.UI?.Log("Гравипушка: рывок " + obj.Material.nameRu);
        }

        // ---------------- hover / info ----------------
        Transform _indicator;
        void UpdateHoverIndicator(Vector3 screenPos)
        {
            if (_indicator == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "HoverIndicator";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
                go.GetComponent<MeshRenderer>().sharedMaterial =
                    RuntimeVisuals.For(Look.Emissive, new Color(0.24f, 0.83f, 0.78f), 0.2f, 0f, 1.6f);
                _indicator = go.transform;
            }
            bool show = CurrentTool is ToolId.Spawn or ToolId.Npc or ToolId.Fire or ToolId.Fluid;
            _indicator.gameObject.SetActive(show && !_pointerDown);
            if (!show) return;
            if (_rig.ScreenToWorld(screenPos, out var p)) _indicator.position = new Vector3(p.x, p.y, 0f);
        }

        void UpdateRaycastTarget(Vector3 screenPos)
        {
            if (!_rig.ScreenToWorld(screenPos, out var point)) return;
            PointerWorld = point;

            if (_dragged != null && Selected != null)
            {
                SelectionDescription = Describe(Selected);
                return;
            }
            var npc = NpcAt(point, 0.9f);
            if (npc != null) { SelectionDescription = "<b>Персонаж</b>\n" + npc.StatusText(); return; }
            var container = ContainerAt(point);
            if (container != null) { SelectionDescription = "<b>Ёмкость</b>\n" + container.StatusText(); return; }
            var obj = ObjectAt(point, 0.7f);
            if (obj != null) { SelectionDescription = Describe(obj); return; }

            SelectionDescription = $"Пусто\nX {point.x:F1} · Y {point.y:F1}";
        }

        static string Describe(SandboxObject o)
        {
            var m = o.Material;
            return $"<b>{m.nameRu}</b> · {m.category}\n" +
                   $"Масса {o.Mass:F1} кг · прочность {m.strength:F0} МПа\n" +
                   $"Твёрдость {m.hardness:F0} МПа · плотность {m.density:F0} кг/м³\n" +
                   $"Температура {o.Temperature:F0} K · HP {o.Health:F0}/{o.MaxHealth:F0}";
        }
    }

    /// <summary>Маркер земли: нужен был для рейкастов, оставлен для совместимости.</summary>
    public class GroundMarker : MonoBehaviour { }
}
