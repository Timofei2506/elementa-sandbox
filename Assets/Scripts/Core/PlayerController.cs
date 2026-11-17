using System;
using System.Collections.Generic;
using System.Linq;
using Elementa.Sim;
using UnityEngine;

namespace Elementa
{
    public enum ToolId
    {
        Select, SpawnMaterial, SpawnFluid, Shoot, Ignite, Heat, Hammer, SpawnNpc, Delete, TimeControl
    }

    /// <summary>
    /// Player input for every tool. Handles picking, dragging, spawning, shooting,
    /// heating, destruction — with a shared code path for mouse and touch.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public ToolId CurrentTool { get; private set; } = ToolId.Select;
        public MaterialDef SelectedMaterial { get; private set; }
        public SubstanceDef SelectedSubstance { get; private set; }
        public BodyShape SelectedShape { get; private set; } = BodyShape.Box;
        public WeaponSystem Weapons { get; private set; }
        public SandboxObject Selected { get; private set; }
        public FluidContainer SelectedContainer { get; private set; }
        public NpcActor SelectedNpc { get; private set; }
        public string SelectionDescription { get; private set; } = "Ничего не выбрано.\nНаведите курсор и нажмите ЛКМ";
        public float HeatPower = 700f;
        public float SpawnScale = 1f;

        SandboxWorld _world;
        CameraRig _rig;
        Rigidbody _dragged;
        Vector3 _dragOffset;
        float _lastUse;
        float _heatAccum;
        bool _pointerDown;
        Vector3 _pointerPos;
        FluidContainer _fluidTarget;

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
            if (tool == ToolId.Shoot && Weapons.Current == null) Weapons.Select("pistol_9mm");
            if (Game.I.UI != null) Game.I.UI.Log("Инструмент: " + ToolName(tool));
        }

        public static string ToolName(ToolId t) => t switch
        {
            ToolId.Select => "Курсор",
            ToolId.SpawnMaterial => "Материал",
            ToolId.SpawnFluid => "Жидкость",
            ToolId.Shoot => "Оружие",
            ToolId.Ignite => "Огонь",
            ToolId.Heat => "Жар",
            ToolId.Hammer => "Молот",
            ToolId.SpawnNpc => "НПС",
            ToolId.Delete => "Разрушение",
            ToolId.TimeControl => "Время",
            _ => t.ToString()
        };

        public void SetMaterial(MaterialDef mat) { SelectedMaterial = mat; CurrentTool = ToolId.SpawnMaterial; }
        public void SetSubstance(SubstanceDef s) { SelectedSubstance = s; CurrentTool = ToolId.SpawnFluid; }
        public void SetShape(BodyShape shape) => SelectedShape = shape;
        public void SetWeapon(WeaponDef w) { Weapons.Select(w.id); CurrentTool = ToolId.Shoot; }

        void Update()
        {
            if (Game.I == null || _world == null) return;
            if (Game.I.UI != null && Game.I.UI.BlocksCameraInput && Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0)) return;

            HandleKeyboardShortcuts();

            bool overUI = Game.I.UI != null && Game.I.UI.BlocksCameraInput;
            bool down = Input.GetMouseButtonDown(0) && !overUI;
            bool held = Input.GetMouseButton(0) && !overUI;
            bool up = Input.GetMouseButtonUp(0);

            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began && !overUI)
            {
                _pointerDown = true;
                _pointerPos = Input.GetTouch(0).position;
                UseTool(_pointerPos, true);
            }
            else if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved && _pointerDown && CurrentTool is ToolId.Heat or ToolId.Hammer or ToolId.Shoot)
            {
                _pointerPos = Input.GetTouch(0).position;
                UseTool(_pointerPos, false);
            }
            else if (Input.touchCount == 1 && (Input.GetTouch(0).phase is TouchPhase.Ended or TouchPhase.Canceled))
            {
                _pointerDown = false;
                EndDrag();
            }
            else if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0) || Input.GetMouseButtonDown(0))
            {
                _pointerPos = Input.mousePosition;
                if (down) UseTool(_pointerPos, true);
                else if (held) UseTool(_pointerPos, false);
                if (up) EndDrag();
            }

            UpdateRaycastTarget(_pointerPos);
        }

        void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ToolId.Select);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ToolId.SpawnMaterial);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ToolId.SpawnFluid);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(ToolId.Shoot);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTool(ToolId.Ignite);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SelectTool(ToolId.Heat);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SelectTool(ToolId.Hammer);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SelectTool(ToolId.SpawnNpc);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SelectTool(ToolId.Delete);
            if (Input.GetKeyDown(KeyCode.Space)) _world.TimeScale = _world.TimeScale > 0.01f ? 0f : 1f;
            if (Input.GetKeyDown(KeyCode.R)) _world.ResetWorld();
            if (Input.GetKeyDown(KeyCode.Delete) && Selected != null) { Destroy(Selected.gameObject); Selected = null; }
            if (Input.GetKeyDown(KeyCode.Return)) Weapons.Refill();
            if (Input.GetKeyDown(KeyCode.Tab) && Game.I.UI != null) Game.I.UI.ToggleCatalog();
        }

        void UseTool(Vector3 screenPos, bool firstPress)
        {
            var ray = _rig.Cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.GetComponentInParent<Projectile>() != null) return;

            switch (CurrentTool)
            {
                case ToolId.Select: HandleSelect(hit, firstPress); break;
                case ToolId.SpawnMaterial: if (firstPress) SpawnMaterialAt(hit.point); break;
                case ToolId.SpawnFluid: if (firstPress) PourFluid(hit); break;
                case ToolId.Shoot: Shoot(ray, firstPress); break;
                case ToolId.Ignite: if (firstPress) IgniteTarget(hit); break;
                case ToolId.Heat: ApplyHeat(hit, firstPress); break;
                case ToolId.Hammer: if (firstPress || Time.time - _lastUse > 0.25f) HammerHit(hit); break;
                case ToolId.SpawnNpc: if (firstPress) SpawnNpc(hit.point); break;
                case ToolId.Delete: if (firstPress || Time.time - _lastUse > 0.12f) DeleteTarget(hit); break;
                case ToolId.TimeControl: if (firstPress) CycleTime(); break;
            }
            _lastUse = Time.time;
        }

        void HandleSelect(RaycastHit hit, bool firstPress)
        {
            var obj = hit.collider.GetComponentInParent<SandboxObject>();
            var npc = hit.collider.GetComponentInParent<NpcActor>();
            var container = hit.collider.GetComponentInParent<FluidContainer>();

            SelectedContainer = container;
            SelectedNpc = npc;
            if (obj != null && firstPress)
            {
                Selected = obj;
                _dragged = obj.Body;
                var groundY = obj.transform.position.y;
                _dragOffset = obj.transform.position - hit.point;
                _dragOffset.y = 0f;
            }
        }

        void EndDrag()
        {
            _dragged = null;
        }

        void UpdateRaycastTarget(Vector3 screenPos)
        {
            if (_dragged != null && Input.GetMouseButton(0))
            {
                // drag along the ground plane, keeping the original height
                if (_rig.ScreenToGround(screenPos, out var ground))
                {
                    var target = ground + _dragOffset;
                    var current = _dragged.position;
                    var vel = (target - current) * 9f;
                    vel.y = _dragged.velocity.y;
                    _dragged.velocity = Vector3.ClampMagnitude(vel, 22f);
                }
            }

            var ray = _rig.Cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return;
            var hovered = hit.collider.GetComponentInParent<SandboxObject>();
            var npc = hit.collider.GetComponentInParent<NpcActor>();
            var container = hit.collider.GetComponentInParent<FluidContainer>();

            if (_dragged != null && Selected != null)
            {
                var m = Selected.Material;
                SelectionDescription = $"<b>{m.nameRu}</b>  ·  {m.category}\n" +
                    $"Масса {Selected.Mass:F1} кг · Прочность {m.strength:F0} МПа\n" +
                    $"Твёрдость {m.hardness:F0} МПа · Плотность {m.density:F0} кг/м³\n" +
                    $"Температура {Selected.Temperature:F0} K · HP {Selected.Health:F0}/{Selected.MaxHealth:F0}";
                return;
            }

            if (npc != null)
            {
                SelectionDescription = "<b>Персонаж</b>\n" + npc.StatusText();
                return;
            }

            if (container != null)
            {
                SelectionDescription = "<b>Ёмкость</b>\n" + container.StatusText();
                return;
            }

            if (hovered != null)
            {
                var m = hovered.Material;
                SelectionDescription = $"<b>{m.nameRu}</b>  ·  {m.category}\n" +
                    $"Масса {hovered.Mass:F1} кг · Прочность {m.strength:F0} МПа\n" +
                    $"Твёрдость {m.hardness:F0} МПа · Теплоёмкость {m.heat:F0}\n" +
                    $"Температура {hovered.Temperature:F0} K · HP {hovered.Health:F0}/{hovered.MaxHealth:F0}";
                return;
            }

            SelectionDescription = $"Поверхность\nТочка {hit.point.x:F1}, {hit.point.y:F1}, {hit.point.z:F1}";
        }

        void SpawnMaterialAt(Vector3 point)
        {
            if (SelectedMaterial == null) return;
            var pos = point + Vector3.up * (0.6f * SpawnScale);
            Vector3 size = SelectedShape switch
            {
                BodyShape.Plate => new Vector3(1.6f, 0.35f, 1.6f) * SpawnScale,
                BodyShape.Sphere => Vector3.one * 1.1f * SpawnScale,
                BodyShape.Cylinder => new Vector3(0.85f, 1.2f, 0.85f) * SpawnScale,
                BodyShape.Capsule => new Vector3(0.7f, 1.1f, 0.7f) * SpawnScale,
                _ => Vector3.one * SpawnScale,
            };
            var obj = _world.Spawn(SelectedMaterial, SelectedShape, size, pos, UnityEngine.Random.Range(0f, 360f));
            if (obj != null) Game.I.UI?.Log($"Создан объект: {SelectedMaterial.nameRu}");
        }

        void PourFluid(RaycastHit hit)
        {
            var container = hit.collider.GetComponentInParent<FluidContainer>();
            if (container != null)
            {
                container.Add(SelectedSubstance.id, 250f);
                Game.I.UI?.Log($"Добавлено 250 мл {SelectedSubstance.nameRu}");
                return;
            }
            // otherwise create a fresh beaker here with the selected substance
            var c = _world.SpawnContainer(hit.point + Vector3.up * 0.7f, 8f);
            c.Fill(SelectedSubstance.id, 2f);
            Game.I.UI?.Log($"Создана ёмкость с {SelectedSubstance.nameRu}");
        }

        void Shoot(Ray ray, bool firstPress)
        {
            if (!firstPress && !Weapons.Current.automatic) return;
            if (Weapons.CanFire)
            {
                var origin = ray.origin + ray.direction * 1.2f;
                Weapons.Fire(origin, ray.direction);
            }
            else if (Weapons.Ammo <= 0)
            {
                Weapons.Refill();
            }
        }

        void IgniteTarget(RaycastHit hit)
        {
            var obj = hit.collider.GetComponentInParent<SandboxObject>();
            var npc = hit.collider.GetComponentInParent<NpcActor>();
            var container = hit.collider.GetComponentInParent<FluidContainer>();
            if (obj != null) { obj.Ignite(); Game.I.UI?.Log($"{obj.Material.nameRu} подожжён"); }
            else if (npc != null) { npc.Ignite(); Game.I.UI?.Log("Персонаж горит"); }
            else if (container != null) { container.Spark(); Game.I.UI?.Log("Ёмкость нагрета до реакции"); }
            else if (hit.collider.GetComponent<GroundMarker>() == null)
            {
                EffectsSystem.SpawnFire(hit.point, Vector3.one * 0.4f, 0.6f, null);
            }
        }

        void ApplyHeat(RaycastHit hit, bool firstPress)
        {
            if (!firstPress && Time.time - _lastUse < 0.05f) return;
            var obj = hit.collider.GetComponentInParent<SandboxObject>();
            var container = hit.collider.GetComponentInParent<FluidContainer>();
            float joules = HeatPower * (firstPress ? 900f : 260f);
            if (obj != null)
            {
                obj.AddHeat(joules);
                EffectsSystem.SpawnSparks(hit.point, hit.normal, 4, 2.5f);
                if (obj.Temperature > 900f) obj.Ignite();
            }
            else if (container != null)
            {
                container.Heat(HeatPower * 0.35f * Time.deltaTime + 25f);
                container.Spark();
            }
            else
            {
                EffectsSystem.SpawnSmoke(hit.point, 0.4f, 0.6f, 0.3f);
            }
        }

        void HammerHit(RaycastHit hit)
        {
            var obj = hit.collider.GetComponentInParent<SandboxObject>();
            var npc = hit.collider.GetComponentInParent<NpcActor>();
            float force = 260f;
            if (obj != null)
            {
                // impact damage scales with hardness of the surface and hammer energy
                float damage = 40f + obj.Material.hardness * 0.02f;
                obj.ApplyDamage(damage, 900f);
                if (obj.Body != null) obj.Body.AddForceAtPosition(hit.normal * -force, hit.point, ForceMode.Impulse);
                EffectsSystem.SpawnSparks(hit.point, hit.normal, 8, 4f);
                ScreenShake.Impact(1f);
            }
            else if (npc != null)
            {
                npc.ApplyDamage(28f, hit.point);
                ScreenShake.Impact(0.8f);
            }
            else if (hit.collider.GetComponent<ArmorPlate>() != null)
            {
                EffectsSystem.SpawnSparks(hit.point, hit.normal, 8, 4f);
            }
        }

        void SpawnNpc(Vector3 point)
        {
            var kinds = new[] { NpcActor.NpcKind.Human, NpcActor.NpcKind.Heavy, NpcActor.NpcKind.Scientist, NpcActor.NpcKind.Zombie, NpcActor.NpcKind.Robot };
            var kind = kinds[UnityEngine.Random.Range(0, kinds.Length)];
            var npc = _world.SpawnNpc(point + Vector3.up * 1.2f, kind);
            npc.SetBehaviour(NpcActor.Behaviour.Wander);
            Game.I.UI?.Log("Создан персонаж: " + kind);
        }

        void DeleteTarget(RaycastHit hit)
        {
            var obj = hit.collider.GetComponentInParent<SandboxObject>();
            var npc = hit.collider.GetComponentInParent<NpcActor>();
            var container = hit.collider.GetComponentInParent<FluidContainer>();
            if (obj != null)
            {
                EffectsSystem.SpawnFluidSplash(obj.transform.position, obj.Material.color, 18, 3f);
                Destroy(obj.gameObject);
            }
            else if (npc != null) npc.Remove();
            else if (container != null) container.BreakOpen();
        }

        void CycleTime()
        {
            float[] scales = { 1f, 0.35f, 0.1f, 0f, 2f };
            int idx = Array.IndexOf(scales, _world.TimeScale);
            _world.TimeScale = scales[(idx + 1) % scales.Length];
            Game.I.UI?.Log("Скорость времени: x" + _world.TimeScale.ToString("0.##"));
        }
    }

    /// <summary>Marker placed on the ground so ignition has a valid raycast target.</summary>
    public class GroundMarker : MonoBehaviour { }
}
