// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2024 yglop <95057024+yglop@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2026 Szyszkrzyneczka <rammus.vult@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Components;

namespace Content.Server._Goobstation.MaterialEnergy
{
    public sealed class MaterialEnergySystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly BatterySystem _batterySystem = default!;
        [Dependency] private readonly StackSystem _stack = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MaterialEnergyComponent, InteractUsingEvent>(OnInteract);
        }

        private void OnInteract(EntityUid uid, MaterialEnergyComponent component, InteractUsingEvent args)
        {
            if (component.MaterialWhiteList == null)
                return;

            _entityManager.TryGetComponent<PhysicalCompositionComponent>(args.Used, out var _composition);
            if (_composition == null)
                return;

            _entityManager.TryGetComponent<StackComponent>(args.Used, out var materialStack);
            if (materialStack == null)
                return;

            foreach (var fueltype in component.MaterialWhiteList)
            {
                if (_composition.MaterialComposition.ContainsKey(fueltype)){
                    if (_batterySystem.GetChargeDifference(uid) == 0)
                        return;

                    var totalMaterial = _composition.MaterialComposition[fueltype] * materialStack.Count;
                    var materialLeft = totalMaterial - _batterySystem.GetChargeDifference(uid);
                    var chargeToAdd = 0;

                    if (materialLeft == 0)
                    {
                        chargeToAdd = totalMaterial;
                        _stack.SetCount(args.Used, 0);
                        args.Handled = true;
                    }
                    else if (materialLeft > 0)
                    {
                        chargeToAdd = Math.Abs(totalMaterial - materialLeft);
                        var toDel = _stack.Split(
                            (EntityUid) args.Used,
                            chargeToAdd / _composition.MaterialComposition[fueltype],
                            Transform(args.Used).Coordinates);
                        QueueDel(toDel);
                    }
                    else
                    {
                        chargeToAdd = Math.Abs(Math.Abs(materialLeft) - _batterySystem.GetChargeDifference(uid));
                        _stack.SetCount(args.Used, 0);
                        args.Handled = true;
                    }

                    _batterySystem.AddCharge(uid, chargeToAdd);
                    return;
                }
            }
        }
    }
}
