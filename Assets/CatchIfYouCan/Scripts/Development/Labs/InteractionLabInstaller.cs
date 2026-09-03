using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Development.Labs
{
    /// <summary>One of every IInteractable in a row, so prompt text, hold duration and reach can be compared side by side.</summary>
    [AddComponentMenu("Catch If You Can/Development/InteractionLabInstaller")]
    public sealed class InteractionLabInstaller : DevelopmentLabInstaller
    {
        public override DevelopmentLab Lab => DevelopmentLab.Interaction;

        private int _built;

        protected override void BuildFixtures()
        {
            BuildFloor(Vector3.zero, new Vector2(20f, 10f));
            BuildMetreGrid(Vector3.zero, 10);
            BuildMarker(PlayerSpawnMarkerName, new Vector3(0f, 0.05f, -3.5f));

            // Spaced two metres apart, which is wider than every interaction distance in the
            // project. A row where two prompts can be in reach at once tests the controller's
            // choice of target rather than the interactables themselves.
            float x = -7f;
            BuildDoor(new Vector3(x, 0f, 1.5f));            x += 2.6f;
            BuildDrawer(new Vector3(x, 0f, 1.5f));          x += 2.6f;
            BuildLightSwitch(new Vector3(x, 0f, 1.5f));     x += 2.6f;
            BuildBreaker(new Vector3(x, 0f, 1.5f));         x += 2.6f;
            BuildHideSpot(new Vector3(x, 0f, 1.5f));        x += 2.6f;
            BuildPickup(new Vector3(x, 0f, 1.5f));
        }

        private void BuildDoor(Vector3 at)
        {
            // The component turns its hinge, and the hinge defaults to its own transform, so
            // the leaf is offset from the pivot rather than centred on it - otherwise the door
            // spins about its middle like a revolving door.
            var hinge = new GameObject("DEV_Door");
            hinge.transform.position = at;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(0.45f, 1f, 0f);
            leaf.transform.localScale = new Vector3(0.9f, 2f, 0.08f);

            hinge.AddComponent<InteractiveDoor>();
            BuildLabel("InteractiveDoor", at + new Vector3(0.45f, 2.2f, 0f));
            _built++;
        }

        private void BuildDrawer(Vector3 at)
        {
            var drawer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drawer.name = "DEV_Drawer";
            drawer.transform.position = at + new Vector3(0f, 0.8f, 0f);
            drawer.transform.localScale = new Vector3(0.7f, 0.25f, 0.5f);
            drawer.AddComponent<InteractiveDrawer>();

            BuildLabel("InteractiveDrawer", at + new Vector3(0f, 1.4f, 0f));
            _built++;
        }

        private void BuildLightSwitch(Vector3 at)
        {
            var lightGo = new GameObject("DEV_SwitchedLight");
            lightGo.transform.position = at + new Vector3(0f, 2.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2f;

            var controller = lightGo.AddComponent<LightController>();
            WireLabField(controller, "lights", new[] { light });

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "DEV_LightSwitch";
            plate.transform.position = at + new Vector3(0f, 1.2f, 0f);
            plate.transform.localScale = new Vector3(0.12f, 0.18f, 0.05f);

            var switchComponent = plate.AddComponent<InteractiveLightSwitch>();
            WireLabField(switchComponent, "lightController", controller);

            BuildLabel("InteractiveLightSwitch", at + new Vector3(0f, 1.5f, 0f));
            _built++;
        }

        private void BuildBreaker(Vector3 at)
        {
            var lightGo = new GameObject("DEV_BreakerLight");
            lightGo.transform.position = at + new Vector3(0f, 2.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.85f, 0.6f);

            var controller = lightGo.AddComponent<LightController>();
            WireLabField(controller, "lights", new[] { light });

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "DEV_BreakerBox";
            box.transform.position = at + new Vector3(0f, 1.4f, 0f);
            box.transform.localScale = new Vector3(0.4f, 0.55f, 0.15f);

            var breaker = box.AddComponent<BreakerBox>();
            WireLabField(breaker, "houseLights", new[] { controller });

            BuildLabel("BreakerBox", at + new Vector3(0f, 1.85f, 0f));
            _built++;
        }

        private void BuildHideSpot(Vector3 at)
        {
            var wardrobe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wardrobe.name = "DEV_HideSpot";
            wardrobe.transform.position = at + new Vector3(0f, 1.1f, 0f);
            wardrobe.transform.localScale = new Vector3(1f, 2.2f, 0.7f);
            wardrobe.AddComponent<HideSpot>();

            BuildLabel("HideSpot", at + new Vector3(0f, 2.4f, 0f));
            _built++;
        }

        private void BuildPickup(Vector3 at)
        {
            var flashlight = Equipment.EquipmentDefinitionFactory.GetById("flashlight");
            var go = new GameObject("DEV_Pickup_Flashlight");
            go.transform.position = at + new Vector3(0f, 0.9f, 0f);

            var trigger = go.AddComponent<SphereCollider>();
            trigger.radius = 0.14f;
            trigger.isTrigger = true;

            var torch = go.AddComponent<Equipment.HeldFlashlight>();
            if (flashlight != null)
                torch.BindDefinition(flashlight);

            go.AddComponent<InteractivePickup>()
              .Configure(torch, "Pick Up Flashlight", destroyWhenTaken: false);

            BuildLabel("InteractivePickup", at + new Vector3(0f, 1.4f, 0f));
            _built++;
        }

        protected override string DescribeState() =>
            "Floor 20x10, 1 m grid, " + _built + " interactables in a row 2.6 m apart.";
    }
}
