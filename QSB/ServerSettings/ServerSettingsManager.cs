﻿using QSB.Utility;
using UnityEngine;

namespace QSB.ServerSettings;

public class ServerSettingsManager : MonoBehaviour, IAddComponentOnStart
{
	public static bool ServerShowPlayerNames;
	public static bool ServerAlwaysShowPlanetIcons;
	public static bool ShowPlayerNames => (ServerShowPlayerNames || QSBCore.IsHost) && QSBCore.ShowPlayerNames;
	public static bool ShowExtraHUD => ShowPlayerNames && QSBCore.ShowExtraHUDElements;
	public static bool AlwaysShowPlanetIcons => (ServerAlwaysShowPlanetIcons || QSBCore.IsHost) && QSBCore.AlwaysShowPlanetIcons;
}
