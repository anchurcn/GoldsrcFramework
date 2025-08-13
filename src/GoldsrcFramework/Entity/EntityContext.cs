using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GoldsrcFramework.Entity
{
    public class EntityContext
    {
        private static IntPtr _hlibcServer = IntPtr.Zero;
        private static IntPtr _errorAllocatorPtr = IntPtr.Zero;
        
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void ErrorAllocator(IntPtr pev)
        {
            Debug.Assert(false, "The entity is not exported in legacy server dll");
            throw new NotImplementedException("The entity is not exported in legacy server dll.");
        }

        /// <summary>
        /// Gets the function pointer to the error allocator function.
        /// </summary>
        private static IntPtr GetErrorAllocatorPtr()
        {
            if (_errorAllocatorPtr == IntPtr.Zero)
            {
                unsafe
                {
                    _errorAllocatorPtr = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&ErrorAllocator;
                }
            }
            return _errorAllocatorPtr;
        }

        public static IntPtr GetLegacyEntityPrivateDataAllocator(string entityClassName)
        {
            // From libserver.dll.
            if (_hlibcServer == IntPtr.Zero)
            {
                _hlibcServer = NativeLibrary.Load("libserver.dll");
                if (_hlibcServer == IntPtr.Zero)
                    throw new Exception("Failed to load libserver.dll. Ensure it is present in the application directory.");
            }

            if (NativeLibrary.TryGetExport(_hlibcServer, entityClassName, out var address))
            {
                return address;
            }
            else
            {
                return GetErrorAllocatorPtr();
            }
        }

        /*
            List of all of hl.dll entities.
        */
        public static string[] GetLegacyEntityList()
        {
            // This method returns a list of all legacy Half-Life entity class names.
            // Extracted from LINK_ENTITY_TO_CLASS macros in the Half-Life source code.
            return new string[]
            {
                "monster_flyer",
                "monster_flyer_flock",
                "monster_alien_grunt",
                "item_airtank",
                "monster_apache",
                "hvr_rocket",
                "monster_barnacle",
                "monster_barney",
                "monster_barney_dead",
                "info_bigmomma",
                "bmortar",
                "monster_bigmomma",
                "monster_bloater",
                "func_wall",
                "func_wall_toggle",
                "func_conveyor",
                "func_illusionary",
                "func_monsterclip",
                "func_rotating",
                "func_pendulum",
                "squidspit",
                "monster_bullchicken",
                "env_global",
                "multisource",
                "func_button",
                "func_rot_button",
                "momentary_rot_button",
                "env_spark",
                "env_debris",
                "button_target",
                "monster_alien_controller",
                "controller_head_ball",
                "controller_energy_ball",
                "crossbow_bolt",
                "weapon_crossbow",
                "ammo_crossbow",
                "weapon_crowbar",
                "func_door",
                "func_water",
                "func_door_rotating",
                "momentary_door",
                "info_target",
                "env_bubbles",
                "beam",
                "trip_beam",
                "env_lightning",
                "env_beam",
                "env_laser",
                "env_glow",
                "env_sprite",
                "gibshooter",
                "env_shooter",
                "test_effect",
                "env_blood",
                "env_shake",
                "env_fade",
                "env_message",
                "env_funnel",
                "env_beverage",
                "item_sodacan",
                "weapon_egon",
                "ammo_egonclip",
                "spark_shower",
                "env_explosion",
                "func_breakable",
                "func_pushable",
                "func_tank",
                "func_tanklaser",
                "func_tankrocket",
                "func_tankmortar",
                "func_tankcontrols",
                "streak_spiral",
                "garg_stomp",
                "monster_gargantua",
                "env_smoker",
                "weapon_gauss",
                "ammo_gaussclip",
                "monster_generic",
                "grenade",
                "weapon_glock",
                "weapon_9mmhandgun",
                "ammo_glockclip",
                "ammo_9mmclip",
                "monster_gman",
                "weapon_handgrenade",
                "monster_human_assassin",
                "monster_headcrab",
                "monster_babycrab",
                "item_healthkit",
                "func_healthcharger",
                "monster_human_grunt",
                "monster_grunt_repel",
                "monster_hgrunt_dead",
                "hornet",
                "weapon_hornetgun",
                "monster_houndeye",
                "func_recharge",
                "monster_cine_scientist",
                "monster_cine_panther",
                "monster_cine_barney",
                "monster_cine2_scientist",
                "monster_cine2_hvyweapons",
                "monster_cine2_slave",
                "monster_cine3_scientist",
                "monster_cine3_barney",
                "cine_blood",
                "cycler",
                "cycler_prdroid",
                "cycler_sprite",
                "cycler_weapon",
                "cycler_wreckage",
                "monster_ichthyosaur",
                "monster_alien_slave",
                "monster_vortigaunt",
                "world_items",
                "item_suit",
                "item_battery",
                "item_antidote",
                "item_security",
                "item_longjump",
                "monster_leech",
                "light",
                "light_spot",
                "light_environment",
                "game_score",
                "game_end",
                "game_text",
                "game_team_master",
                "game_team_set",
                "game_zone_player",
                "game_player_hurt",
                "game_counter",
                "game_counter_set",
                "game_player_equip",
                "game_player_team",
                "monstermaker",
                "func_mortar_field",
                "monster_mortar",
                "weapon_mp5",
                "weapon_9mmAR",
                "ammo_mp5clip",
                "ammo_9mmAR",
                "ammo_9mmbox",
                "ammo_mp5grenades",
                "ammo_ARgrenades",
                "monster_nihilanth",
                "nihilanth_energy_ball",
                "info_node",
                "info_node_air",
                "testhull",
                "node_viewer",
                "node_viewer_human",
                "node_viewer_fly",
                "node_viewer_large",
                "monster_osprey",
                "path_corner",
                "path_track",
                "func_plat",
                "func_platrot",
                "func_train",
                "func_tracktrain",
                "func_traincontrols",
                "func_trackchange",
                "func_trackautochange",
                "func_guntarget",
                "player",
                "monster_hevsuit_dead",
                "player_weaponstrip",
                "player_loadsaved",
                "info_intermission",
                "weapon_python",
                "weapon_357",
                "ammo_357",
                "monster_rat",
                "monster_cockroach",
                "weapon_rpg",
                "laser_spot",
                "rpg_rocket",
                "ammo_rpgclip",
                "monster_satchel",
                "weapon_satchel",
                "monster_scientist",
                "monster_scientist_dead",
                "monster_sitting_scientist",
                "scripted_sequence",
                "aiscripted_sequence",
                "scripted_sentence",
                "monster_furniture",
                "weapon_shotgun",
                "ammo_buckshot",
                "ambient_generic",
                "env_sound",
                "speaker",
                "soundent",
                "monster_snark",
                "weapon_snark",
                "info_null",
                "info_player_deathmatch",
                "info_player_start",
                "info_landmark",
                "DelayedUse",
                "my_monster",
                "monster_tentacle",
                "monster_tentaclemaw",
                "func_friction",
                "trigger_auto",
                "trigger_relay",
                "multi_manager",
                "env_render",
                "trigger",
                "trigger_hurt",
                "trigger_monsterjump",
                "trigger_cdaudio",
                "target_cdaudio",
                "trigger_multiple",
                "trigger_once",
                "trigger_counter",
                "trigger_transition",
                "fireanddie",
                "trigger_changelevel",
                "func_ladder",
                "trigger_push",
                "trigger_teleport",
                "info_teleport_destination",
                "trigger_autosave",
                "trigger_endsection",
                "trigger_gravity",
                "trigger_changetarget",
                "trigger_camera",
                "monster_tripmine",
                "weapon_tripmine",
                "monster_turret",
                "monster_miniturret",
                "monster_sentry",
                "func_vehicle",
                "func_vehiclecontrols",
                "weaponbox",
                "infodecal",
                "bodyque",
                "worldspawn",
                "xen_plantlight",
                "xen_hair",
                "xen_ttrigger",
                "xen_tree",
                "xen_spore_small",
                "xen_spore_medium",
                "xen_spore_large",
                "xen_hull",
                "monster_zombie"
            };
        }
        public static string[] GetEntityList()
        {
            return GetLegacyEntityList();
        }
    }

}
