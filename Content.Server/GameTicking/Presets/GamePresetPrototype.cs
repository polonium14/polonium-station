using Content.Server.Maps;
using Content.Shared._Polonium.GameTicking;
using Content.Shared._Polonium.Survey;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Presets
{
    /// <summary>
    ///     A round-start setup preset, such as which antagonists to spawn.
    /// </summary>
    [Prototype]
    public sealed partial class GamePresetPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField]
        public string Description = string.Empty;

        [DataField]
        public bool ShowInVote;

        [DataField]
        public int? MinPlayers;

        [DataField]
        public int? MaxPlayers;

        [DataField]
        public IReadOnlyList<EntProtoId> Rules { get; private set; } = Array.Empty<EntProtoId>();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps")]
        public ProtoId<GameMapPoolPrototype>? MapPool;

        // POLONIUM START
        /// <summary>
        /// Round moods this preset belongs to and its weight in each one. Secret picks from these.
        /// </summary>
        [DataField]
        public Dictionary<ProtoId<RoundMoodPrototype>, float> Moods = new();

        /// <summary>
        /// The answers this preset aims for in two-sided survey questions, replacing the question's own target.
        /// </summary>
        [DataField]
        public Dictionary<ProtoId<RoundSurveyQuestionPrototype>, float> SurveyTargets = new();
        // POLONIUM END
    }
}
