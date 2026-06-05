// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <ozzeusz@gmail.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.CallablePhone;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Polonium.CallablePhone;

[UsedImplicitly]
public sealed class CallablePhoneBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    [ViewVariables]
    private CallablePhoneWindow? _window;

    public CallablePhoneBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var phone = Owner;
        if (EntMan.TryGetComponent<TelephoneHandsetComponent>(Owner, out var handset))
            phone = EntMan.GetEntity(handset.ParentPhone);

        if (!EntMan.EntityExists(phone) || !EntMan.HasComponent<CallablePhoneComponent>(phone))
        {
            Close();
            return;
        }

        _window = this.CreateWindow<CallablePhoneWindow>();
        _window.InitializeDependencies(_entitySystemManager.DependencyCollection);

        _window.Title = Loc.GetString("callable-phone-window-title", ("title", EntMan.GetComponent<MetaDataComponent>(phone).EntityName));
        _window.SetOwner(phone);
        _window.UpdateState(new Dictionary<NetEntity, string>());

        _window.CallPressed += receiver => SendMessage(new CallablePhoneCallMessage(receiver));
        _window.AnswerPressed += () => SendMessage(new CallablePhoneAnswerMessage());
        _window.HangUpPressed += () => SendMessage(new CallablePhoneHangUpMessage());

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CallablePhoneBoundInterfaceState castState)
            return;

        _window?.UpdateState(castState.Phones);
    }
}
