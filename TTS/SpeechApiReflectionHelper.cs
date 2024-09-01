using System;
using System.Collections;
using System.Reflection;
using System.Speech.Synthesis;

namespace DaleGhent.NINA.GroundStation.TTS {

    /// <summary>
    /// The need for this class is due to the fact that the OneCore voices are not enumerated by the System.Speech.Synthesis.SpeechSynthesizer class.
    /// I found this solution on StackOverflow: https://stackoverflow.com/a/71198211 . It uses reflection to inject the OneCore voices into the SpeechSynthesizer object so that the GetInstalledVoices method returns them.
    /// The OneCore voices are what one sees listed in the Windows 10 Settings app under "Time & Language" -> "Speech" -> "Manage voices". The old non-OneCore voices are listed in the Control Panel under "Ease of Access" -> "Speech Recognition" -> "Text to Speech".
    /// </summary>

    public static class SpeechApiReflectionHelper {
        private const string PROP_VOICE_SYNTHESIZER = "VoiceSynthesizer";
        private const string FIELD_INSTALLED_VOICES = "_installedVoices";

        private const string ONE_CORE_VOICES_REGISTRY = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Voices";

        private static readonly Type ObjectTokenCategoryType = typeof(SpeechSynthesizer).Assembly
            .GetType("System.Speech.Internal.ObjectTokens.ObjectTokenCategory")!;

        private static readonly Type VoiceInfoType = typeof(SpeechSynthesizer).Assembly
            .GetType("System.Speech.Synthesis.VoiceInfo")!;

        private static readonly Type InstalledVoiceType = typeof(SpeechSynthesizer).Assembly
            .GetType("System.Speech.Synthesis.InstalledVoice")!;

        public static void InjectOneCoreVoices(this SpeechSynthesizer synthesizer) {
            var voiceSynthesizer = GetProperty(synthesizer, PROP_VOICE_SYNTHESIZER) ??
                throw new NotSupportedException($"Property not found: {PROP_VOICE_SYNTHESIZER}");

            if (GetField(voiceSynthesizer, FIELD_INSTALLED_VOICES) is not IList installedVoices) {
                throw new NotSupportedException($"Field not found or null: {FIELD_INSTALLED_VOICES}");
            }

            if (ObjectTokenCategoryType
                    .GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)?
                    .Invoke(null, [ONE_CORE_VOICES_REGISTRY]) is not IDisposable otc) {
                throw new NotSupportedException($"Failed to call Create on {ObjectTokenCategoryType} instance");
            }

            using (otc) {
                if (ObjectTokenCategoryType
                        .GetMethod("FindMatchingTokens", BindingFlags.Instance | BindingFlags.NonPublic)?
                        .Invoke(otc, [null, null]) is not IList tokens) {
                    throw new NotSupportedException($"Failed to list matching tokens");
                }

                foreach (var token in tokens) {
                    if (token == null || GetProperty(token, "Attributes") == null) continue;

                    var voiceInfo =
                        typeof(SpeechSynthesizer).Assembly
                            .CreateInstance(VoiceInfoType.FullName!, true,
                                BindingFlags.Instance | BindingFlags.NonPublic, null,
                                [token], null, null) ?? throw new NotSupportedException($"Failed to instantiate {VoiceInfoType}");
                    var installedVoice =
                        typeof(SpeechSynthesizer).Assembly
                            .CreateInstance(InstalledVoiceType.FullName!, true,
                                BindingFlags.Instance | BindingFlags.NonPublic, null,
                                [voiceSynthesizer, voiceInfo], null, null) ?? throw new NotSupportedException($"Failed to instantiate {InstalledVoiceType}");

                    installedVoices.Add(installedVoice);
                }
            }
        }

        private static object? GetProperty(object target, string propName) => target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

        private static object? GetField(object target, string propName) => target.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }
}