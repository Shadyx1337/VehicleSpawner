﻿using CitizenFX.Core;
using FivePD.API;
using System;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;
using Newtonsoft.Json.Linq;
using FivePD.API.Utils;
using System.Collections.Generic;
using MenuAPI;
using System.Linq;


#pragma warning disable 1998
namespace IntuitiveMenus
{
    class Trunk
    {
        string AnimDict = "mini@repair";

        internal async Task OpenTrunk()
        {
            float triggerDistance = 1.0f;

            // Find entities in front of player
            Vector3 rayEndCoords = GetOffsetFromEntityInWorldCoords(PlayerPedId(), 0.0f, triggerDistance + 3, -0.7f);

            int rayHandle = StartShapeTestRay(Game.PlayerPed.Position.X, Game.PlayerPed.Position.Y, Game.PlayerPed.Position.Z, rayEndCoords.X, rayEndCoords.Y, rayEndCoords.Z, 2, PlayerPedId(), 0);
            bool _Hit = false;
            int _vehicleHandle = 0;
            Vector3 _endCoords = new Vector3();
            Vector3 _surfaceNormal = new Vector3();

            GetShapeTestResult(rayHandle, ref _Hit, ref _endCoords, ref _surfaceNormal, ref _vehicleHandle);

            // Check if the entity hit is an emergency vehicle (class 18)
            if (DoesEntityExist(_vehicleHandle) && GetVehicleClass(_vehicleHandle) == 18)
            {
                // Find the back of the vehicle and check if it's within the trigger distance
                Vector3 offsetFromVehicle = GetOffsetFromEntityGivenWorldCoords(_vehicleHandle, Game.PlayerPed.Position.X, Game.PlayerPed.Position.Y, Game.PlayerPed.Position.Z);
                Vector3 vehicleDimensionsMinimum = new Vector3();
                Vector3 vehicleDimensionsMaximum = new Vector3();
                GetModelDimensions((uint)GetEntityModel(_vehicleHandle), ref vehicleDimensionsMinimum, ref vehicleDimensionsMaximum);

                Vector3 trunkOffset = vehicleDimensionsMinimum - offsetFromVehicle;

                if (trunkOffset.X < triggerDistance && trunkOffset.Y < triggerDistance && trunkOffset.Z < triggerDistance && trunkOffset.Y > -0.25)
                {
                    if (Utilities.IsPlayerOnDuty())
                    {
                        // Request the animation dictionary and wait for it to be loaded
                        RequestAnimDict(AnimDict);

                        int maxretries = 0;
                        while (!HasAnimDictLoaded(AnimDict) && maxretries < 10)
                        {
                            await BaseScript.Delay(25);
                            maxretries++;
                        }

                        // Check if the trunk is open or closed and act accordingly
                        if (GetVehicleDoorAngleRatio(_vehicleHandle, 5) > 0)
                        {
                            SetVehicleDoorShut(_vehicleHandle, 5, false);
                            StopAnimTask(PlayerPedId(), AnimDict, "fixing_a_ped", 4f);
                        }
                        else
                        {
                            _ = OpenMenu(_vehicleHandle);

                            SetCurrentPedWeapon(PlayerPedId(), (uint)GetHashKey("WEAPON_UNARMED"), true);
                            SetVehicleDoorOpen(_vehicleHandle, 5, false, false);

                            if (HasAnimDictLoaded(AnimDict))
                            {
                                TaskPlayAnim(
                                    PlayerPedId(), // ped
                                    AnimDict, // Anim Dictionary
                                    "fixing_a_ped", // Animation
                                    4.0f, // Blend in speed
                                    4.0f, // Blend out speed
                                    -1, // Duration
                                    1, // Flag
                                    0.5f, // Playback Rate
                                    false, // Lock X
                                    false, // Lock Y
                                    false // Lock Z
                                );
                            }
                            await BaseScript.Delay(100);
                            SetEntityNoCollisionEntity(PlayerPedId(), _vehicleHandle, true);
                        }
                    }
                    else
                    {
                        Common.DisplayNotification("You need to be on duty to access the trunk!");
                    }
                }
                else if (IsEntityPlayingAnim(PlayerPedId(), AnimDict, "fixing_a_ped", 3))
                {
                    StopAnimTask(PlayerPedId(), AnimDict, "fixing_a_ped", 4f);
                }
            }
        }

        internal async Task OpenMenu(int vehicleHandle)
        {
            PlayerData playerData = Utilities.GetPlayerData();

            Menu menu = new Menu("Trunk");
            MenuController.AddMenu(menu);

            MenuItem menuItem_SpikeStrips = new MenuItem("Get spike strip");
            if (SpikeStripVehicles.Contains(GetEntityModel(vehicleHandle)))
            {
                menu.AddMenuItem(menuItem_SpikeStrips);
            }

            // Check which loadouts are available for the player in the trunk and create the menu buttons for it
            foreach (Loadout _Loadout in Loadouts)
            {
                bool _isAllowed = false;

                if (_Loadout.IsAvailableForEveryone) _isAllowed = true;
                else if (_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForRanks.Contains(playerData.Rank)) _isAllowed = true;
                }
                else if (!_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForDepartments.Contains(playerData.DepartmentID)) _isAllowed = true;
                    else if (_Loadout.AvailableForDepartments.Count == 1) _isAllowed = true;
                }


                if (_isAllowed)
                {
                    bool _missesWeapon = false;
                    foreach (var _Weapon in _Loadout.Weapons)
                    {
                        if (!_missesWeapon && !HasPedGotWeapon(PlayerPedId(), (uint)GetHashKey(_Weapon.Model), false)) _missesWeapon = true;
                    }
                    MenuItem _menuButton = new MenuItem((_missesWeapon ? "Take" : "Put back") + " " + _Loadout.Name);
                    _menuButton.ItemData = new Tuple<bool, List<Weapon>>(_missesWeapon, _Loadout.Weapons);
                    menu.AddMenuItem(_menuButton);
                }
            }

            MenuItem menuItem_RefillAmmo = new MenuItem("Refill Ammo");
            menuItem_RefillAmmo.ItemData = new Dictionary<string, int>();
            // Iterate through normal loadouts
            foreach (Loadout _Loadout in Common.Loadouts)
            {
                bool _isAllowed = false;

                if (_Loadout.IsAvailableForEveryone) _isAllowed = true;
                else if (_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForRanks.Contains(playerData.Rank)) _isAllowed = true;
                }
                else if (!_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForDepartments.Contains(playerData.DepartmentID)) _isAllowed = true;
                }
                if (_isAllowed)
                {
                    foreach (Weapon _Weapon in _Loadout.Weapons)
                    {
                        menuItem_RefillAmmo.ItemData[_Weapon.Model] = _Weapon.Ammo;
                    }
                }
            }
            // Iterate through trunk loadouts
            foreach (Loadout _Loadout in Loadouts)
            {
                bool _isAllowed = false;

                if (_Loadout.IsAvailableForEveryone) _isAllowed = true;
                else if (_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForRanks.Contains(playerData.Rank)) _isAllowed = true;
                }
                else if (!_Loadout.UseRanks)
                {
                    if (_Loadout.AvailableForDepartments.Contains(playerData.DepartmentID)) _isAllowed = true;
                    else if (_Loadout.AvailableForDepartments.Count == 1) _isAllowed = true;
                }

                if (_isAllowed)
                {
                    foreach (Weapon _Weapon in _Loadout.Weapons)
                    {
                        menuItem_RefillAmmo.ItemData[_Weapon.Model] = _Weapon.Ammo;
                    }
                }
            }
            menu.AddMenuItem(menuItem_RefillAmmo);

            menu.OnItemSelect += (_menu, _item, _index) =>
            {
                if (_item.Index == menuItem_SpikeStrips.Index)
                {
                    SetControlNormal(0, 21, 2.0f);
                    SetControlNormal(0, 38, 1.0f);
                    _item.Text = "Spike strips added";
                    _item.Enabled = false;
                }
                else if (_item.Index == menuItem_RefillAmmo.Index)
                {
                    Dictionary<string, int> _itemData = _item.ItemData;

                    foreach (KeyValuePair<string, int> _RefillItem in _itemData)
                    {
                        uint _weaponHash = (uint)GetHashKey(_RefillItem.Key);

                        if (GetAmmoInPedWeapon(PlayerPedId(), _weaponHash) < _RefillItem.Value) SetPedAmmo(PlayerPedId(), _weaponHash, _RefillItem.Value);
                    }
                }
                else
                {
                    // Give the weapon to the player
                    Tuple<bool, List<Weapon>> _ItemData = _item.ItemData;

                    if (_ItemData.Item1)
                    {
                        foreach (Weapon _Weapon in _ItemData.Item2)
                        {
                            uint _weaponHash = (uint)GetHashKey(_Weapon.Model);

                            GiveWeaponToPed(PlayerPedId(), _weaponHash, _Weapon.Ammo, false, true);
                            SetPedAmmo(PlayerPedId(), _weaponHash, _Weapon.Ammo); // Need to call this; GiveWeaponToPed always adds ammo up

                            if (_Weapon.Components.Length > 0)
                            {
                                foreach (string _weaponComponent in _Weapon.Components)
                                {
                                    GiveWeaponComponentToPed(PlayerPedId(), _weaponHash, (uint)GetHashKey(_weaponComponent));
                                }
                            }
                        }
                        _item.Text = _item.Text.Replace("Take", "Put back");

                    }
                    else
                    {
                        foreach (Weapon _Weapon in _ItemData.Item2)
                        {
                            uint _weaponHash = (uint)GetHashKey(_Weapon.Model);
                            RemoveWeaponFromPed(PlayerPedId(), _weaponHash);
                        }
                        _item.Text = _item.Text.Replace("Put back", "Take");
                    }

                    _item.ItemData = new Tuple<bool, List<Weapon>>(!_ItemData.Item1, _ItemData.Item2);
                }
            };

            // Stop animation and close the trunk when player exits vehicle
            menu.OnMenuClose += (_menu) =>
            {
                StopAnimTask(PlayerPedId(), AnimDict, "fixing_a_ped", 4f);
                SetVehicleDoorShut(vehicleHandle, 5, false);
                vehicleHandle = 0;
            };

            menu.OpenMenu();
        }

        internal List<Loadout> Loadouts = new List<Loadout>();
        internal List<int> SpikeStripVehicles = new List<int>();
    }
}