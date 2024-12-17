﻿using HarmonyLib;
using QSB.Inputs;
using QSB.Messaging;
using QSB.Patches;
using QSB.TimeSync.Messages;
using QSB.Utility;
using UnityEngine;

namespace QSB.TimeSync.Patches;

[HarmonyPatch]
public class TimePatches : QSBPatch
{
	public override QSBPatchTypes Type => QSBPatchTypes.OnClientConnect;

	/// <summary>
	/// prevents wakeup prompt since we automatically wake you up.
	/// (doesn't happen for host because we don't patch until TimeLoop._initialized i.e. after Start)
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerCameraEffectController), nameof(PlayerCameraEffectController.OnStartOfTimeLoop))]
	public static bool PlayerCameraEffectController_OnStartOfTimeLoop() => false;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerCameraEffectController), nameof(PlayerCameraEffectController.WakeUp))]
	public static void PlayerCameraEffectController_WakeUp(PlayerCameraEffectController __instance)
	{
		// prevent funny thing when you pause while waking up
		Locator.GetPauseCommandListener().AddPauseCommandLock();
		Delay.RunWhen(() => !__instance._isOpeningEyes, () => Locator.GetPauseCommandListener().RemovePauseCommandLock());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(OWTime), nameof(OWTime.Pause))]
	public static bool StopPausing(ref OWTime.PauseType pauseType)
	{
		if (pauseType is OWTime.PauseType.Initializing
			or OWTime.PauseType.Streaming
			or OWTime.PauseType.Loading)
		{
			return true;
		}
		else
		{
			// stop NomaiVR from pausing manually grrrrrrrrrrr
			pauseType = OWTime.PauseType.Menu;
			return false;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SubmitActionSkipToNextLoop), nameof(SubmitActionSkipToNextLoop.AdvanceToNewTimeLoop))]
	public static void PreventMeditationSoftlock()
		=> OWInput.ChangeInputMode(InputMode.Character);
}

public class ClientTimePatches : QSBPatch
{
	public override QSBPatchTypes Type => QSBPatchTypes.OnNonServerClientConnect;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.SetSecondsRemaining))]
	private static void SetSecondsRemaining(float secondsRemaining)
	{
		if (Remote)
		{
			return;
		}
		new SetSecondsRemainingMessage(secondsRemaining).Send();
	}
}

public class TimeLoopPatches : QSBPatch
{
	public override QSBPatchTypes Type => QSBPatchTypes.OnModStart;

	public static bool IsLoopAfterStatue;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TimeLoop), nameof(TimeLoop.Start))]
	private static void TimeLoopStart()
	{
		IsLoopAfterStatue = TimeLoop._startTimeLoopOnReload;
	}
}
