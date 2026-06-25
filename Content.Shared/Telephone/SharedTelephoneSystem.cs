// SPDX-FileCopyrightText: 2024 chromiumboy <50505512+chromiumboy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <ozzeusz@gmail.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Shared.Telephone;

public abstract class SharedTelephoneSystem : EntitySystem
{
    public bool IsTelephoneEngaged(Entity<TelephoneComponent> entity)
    {
        return entity.Comp.LinkedTelephones.Any();
    }

    public string GetFormattedCallerIdForEntity(string? presumedName, string? presumedJob, Color fontColor, string fontType = "Default", int fontSize = 12)
    {
        var callerId = Loc.GetString("chat-telephone-unknown-caller",
            ("color", fontColor),
            ("fontType", fontType),
            ("fontSize", fontSize));

        if (presumedName == null)
            return callerId;

        if (presumedJob != null)
            callerId = Loc.GetString("chat-telephone-caller-id-with-job",
                ("callerName", presumedName),
                ("callerJob", presumedJob),
                ("color", fontColor),
                ("fontType", fontType),
                ("fontSize", fontSize));

        else
            callerId = Loc.GetString("chat-telephone-caller-id-without-job",
                ("callerName", presumedName),
                ("color", fontColor),
                ("fontType", fontType),
                ("fontSize", fontSize));

        return callerId;
    }

    public string GetFormattedDeviceIdForEntity(string? deviceName, Color fontColor, string fontType = "Default", int fontSize = 12)
    {
        if (deviceName == null)
        {
            return Loc.GetString("chat-telephone-unknown-device",
                ("color", fontColor),
                ("fontType", fontType),
                ("fontSize", fontSize));
        }

        return Loc.GetString("chat-telephone-device-id",
            ("deviceName", deviceName),
            ("color", fontColor),
            ("fontType", fontType),
            ("fontSize", fontSize));
    }
}
