﻿using Mirror;
using OWML.Common;
using OWML.Utils;
using QSB.Animation.Player.Messages;
using QSB.Animation.Player.Thrusters;
using QSB.Messaging;
using QSB.Player;
using QSB.Utility;
using QSB.WorldSync.Messages;
using System;
using UnityEngine;

namespace QSB.Animation.Player;

[UsedInUnityProject]
public class AnimationSync : PlayerSyncObject
{
	public static int DropHeldItem = Animator.StringToHash("DropHeldItem");
	public static int HoldLantern = Animator.StringToHash("HoldLantern");
	public static int HoldSharedStone = Animator.StringToHash("HoldSharedStone");
	public static int HoldScroll = Animator.StringToHash("HoldScroll");
	public static int HoldWarpCore = Animator.StringToHash("HoldWarpCore");
	public static int HoldAdvWarpCore = Animator.StringToHash("HoldAdvWarpCore");
	public static int HoldItem = Animator.StringToHash("HoldItem");

	private RuntimeAnimatorController _suitedAnimController;
	private AnimatorOverrideController _unsuitedAnimController;
	private GameObject _suitedGraphics;
	private GameObject _unsuitedGraphics;

	public AnimatorMirror Mirror { get; private set; }
	public bool InSuitedUpState { get; set; }
	public Animator VisibleAnimator { get; private set; }
	public Animator InvisibleAnimator { get; private set; }
	public NetworkAnimator NetworkAnimator { get; private set; }

	protected void Awake()
	{
		InvisibleAnimator = gameObject.GetRequiredComponent<Animator>();
		NetworkAnimator = gameObject.GetRequiredComponent<NetworkAnimator>();
		NetworkAnimator.enabled = false;
		RequestInitialStatesMessage.SendInitialState += SendInitialState;
	}

	protected void OnDestroy()
	{
		RequestInitialStatesMessage.SendInitialState -= SendInitialState;
		GlobalMessenger.RemoveListener("EnableBigHeadMode", new Callback(OnEnableBigHeadMode));
	}

	/// <summary>
	/// This wipes the NetworkAnimator's fields, so it assumes the parameters have changed.
	/// Basically just forces it to set all its dirty flags.
	/// BUG: this doesnt work for other players because its only called by the host.
	/// </summary>
	private void SendInitialState(uint to) => NetworkAnimator.Invoke("Awake");

	public void Reset() => InSuitedUpState = false;

	private void InitCommon(Transform modelRoot)
	{
		try
		{
			if (modelRoot == null)
			{
				DebugLog.ToConsole("Error - Trying to InitCommon with null body!", MessageType.Error);
				return;
			}

			VisibleAnimator = modelRoot.GetComponent<Animator>();
			Mirror = modelRoot.gameObject.AddComponent<AnimatorMirror>();
			if (isLocalPlayer)
			{
				Mirror.Init(VisibleAnimator, InvisibleAnimator, NetworkAnimator);
			}
			else
			{
				Mirror.Init(InvisibleAnimator, VisibleAnimator, null);
			}

			NetworkAnimator.enabled = true;
			NetworkAnimator.Invoke("Awake");

			_suitedAnimController = Instantiate(QSBCore.NetworkAssetBundle.LoadAsset<RuntimeAnimatorController>("Assets/GameAssets/AnimatorController/Player.controller"));
			_unsuitedAnimController = Instantiate(QSBCore.NetworkAssetBundle.LoadAsset<AnimatorOverrideController>("Assets/GameAssets/AnimatorOverrideController/PlayerUnsuitedOverride.overrideController"));
			_suitedGraphics = modelRoot.GetChild(1).gameObject;
			_unsuitedGraphics = modelRoot.GetChild(0).gameObject;

			VisibleAnimator.SetLayerWeight(2, 1f);
		}
		catch (Exception ex)
		{
			DebugLog.ToConsole($"Exception thrown when running InitCommon on {(modelRoot != null ? modelRoot.name : "NULL BODY")}. {ex.Message} Stacktrace: {ex.StackTrace}", MessageType.Error);
		}
	}

	public void InitLocal(Transform body)
	{
		InitCommon(body);
		InitAccelerationSync();
	}

	public void InitRemote(Transform body)
	{
		InitCommon(body);
		SetSuitState(QSBSceneManager.CurrentScene == OWScene.EyeOfTheUniverse);
		InitAccelerationSync();
		ThrusterManager.CreateRemotePlayerVFX(Player);
		ThrusterManager.CreateRemotePlayerSFX(Player);

		Delay.RunWhen(() => Player.CameraBody != null,
			() => body.GetComponent<PlayerHeadRotationSync>().Init(Player.CameraBody.transform));

		GlobalMessenger.AddListener("EnableBigHeadMode", new Callback(OnEnableBigHeadMode));
	}

	private void InitAccelerationSync()
	{
		Player.JetpackAcceleration = GetComponent<JetpackAccelerationSync>();
		var thrusterModel = isOwned ? Locator.GetPlayerBody().GetComponent<ThrusterModel>() : null;
		Player.JetpackAcceleration.Init(thrusterModel);
	}

	private void OnEnableBigHeadMode()
	{
		var bone = VisibleAnimator.GetBoneTransform(HumanBodyBones.Head);
		bone.localScale = new Vector3(2.5f, 2.5f, 2.5f);
	}

	public void SetSuitState(bool suitedUp)
	{
		if (!Player.IsReady)
		{
			return;
		}

		if (Player == QSBPlayerManager.LocalPlayer)
		{
			new PlayerSuitMessage(suitedUp).Send();
		}

		if (InSuitedUpState == suitedUp)
		{
			return;
		}

		InSuitedUpState = suitedUp;
		if (_unsuitedAnimController == null)
		{
			DebugLog.ToConsole($"Error - Unsuited controller is null. ({PlayerId})", MessageType.Error);
		}

		if (_suitedAnimController == null)
		{
			DebugLog.ToConsole($"Error - Suited controller is null. ({PlayerId})", MessageType.Error);
		}

		if (_unsuitedGraphics == null)
		{
			DebugLog.ToConsole($"Warning - _unsuitedGraphics is null! ({PlayerId})", MessageType.Warning);
		}

		if (_suitedGraphics == null)
		{
			DebugLog.ToConsole($"Warning - _suitedGraphics is null! ({PlayerId})", MessageType.Warning);
		}

		var controller = suitedUp ? _suitedAnimController : _unsuitedAnimController;
		if (_unsuitedGraphics != null)
		{
			_unsuitedGraphics?.SetActive(!suitedUp);
		}

		if (_suitedGraphics != null)
		{
			_suitedGraphics?.SetActive(suitedUp);
		}

		if (InvisibleAnimator == null)
		{
			DebugLog.ToConsole($"Error - InvisibleAnimator is null. ({PlayerId})", MessageType.Error);
		}
		else
		{
			InvisibleAnimator.runtimeAnimatorController = controller;
		}

		if (VisibleAnimator == null)
		{
			DebugLog.ToConsole($"Error - VisibleAnimator is null. ({PlayerId})", MessageType.Error);
		}
		else
		{
			VisibleAnimator.runtimeAnimatorController = controller;
		}

		// Avoids "jumping" when putting on suit
		if (VisibleAnimator != null)
		{
			VisibleAnimator.SetBool("Grounded", true);
		}

		if (InvisibleAnimator != null)
		{
			InvisibleAnimator.SetBool("Grounded", true);
		}

		if (NetworkAnimator == null)
		{
			DebugLog.ToConsole($"Error - NetworkAnimator is null. ({PlayerId})", MessageType.Error);
		}
		else if (Mirror == null)
		{
			DebugLog.ToConsole($"Error - Mirror is null. ({PlayerId})", MessageType.Error);
		}

		Mirror.RebuildFloatParams();
		NetworkAnimator.Invoke("Awake");
	}

	public void PickUpItem(int animHash)
	{
		VisibleAnimator.SetTrigger(animHash);
		VisibleAnimator.SetLayerWeight(2, 1);
		InvisibleAnimator.SetTrigger(animHash);
		InvisibleAnimator.SetLayerWeight(2, 1);
	}

	public void DropItem()
	{
		VisibleAnimator.SetTrigger(DropHeldItem);
		VisibleAnimator.SetLayerWeight(2, 1);
		InvisibleAnimator.SetTrigger(DropHeldItem);
		InvisibleAnimator.SetLayerWeight(2, 1);
	}
}
