
using GoldsrcFramework.Engine.Native;
using GoldsrcFramework.LinearMath;
using System.Runtime.InteropServices;

/// <summary>
/// Example demonstrating how to use the generated engine types
/// </summary>
public unsafe class TypeUsageExample: FrameworkClientExports
{
    /// <summary>
    /// Example: Working with Vector3f
    /// </summary>
    public void VectorExample()
    {
        // Create vectors
        var origin = new Vector3(100.0f, 200.0f, 50.0f);
        var velocity = new Vector3(0.0f, 0.0f, -9.8f);

        // Vector operations
        var newPosition = new Vector3(
            origin.X + velocity.X,
            origin.Y + velocity.Y,
            origin.Z + velocity.Z
        );
    }

    /// <summary>
    /// Example: Working with primitive type wrappers
    /// </summary>
    public void PrimitiveWrapperExample()
    {
        // HSPRITE usage
        var sprite = new HSPRITE { Value = 42 };

        // qboolean usage
        var isVisible = new qboolean { Value = 1 }; // true
        var isHidden = new qboolean { Value = 0 };  // false
    }

    /// <summary>
    /// Example: Working with entity state
    /// </summary>
    public void EntityStateExample()
    {
        var entityState = new entity_state_s
        {
            entityType = 1,
            number = 100,
            origin = new Vector3(0, 0, 0),
            angles = new Vector3(0, 90, 0),
            modelindex = 1,
            sequence = 0,
            frame = 0.0f,
            health = 100,
            team = 1
        };

        // Access user fields
        entityState.iuser1 = 42;
        entityState.fuser1 = 3.14f;
        entityState.vuser1 = new Vector3(1, 2, 3);
    }

    /// <summary>
    /// Example: Working with client data
    /// </summary>
    public void ClientDataExample()
    {
        var clientData = new clientdata_s
        {
            origin = new Vector3(100, 200, 50),
            velocity = new Vector3(0, 0, -9.8f),
            health = 100.0f,
            fov = 90.0f,
            maxspeed = 320.0f,
            waterlevel = 0,
            flags = 1 // FL_ONGROUND
        };

        // Access mod-specific fields
        clientData.iuser1 = 1;
        clientData.fuser1 = 1.5f;
        clientData.vuser1 = new Vector3(0, 0, 1);
    }

    /// <summary>
    /// Example: Working with user commands
    /// </summary>
    public void UserCommandExample()
    {
        var userCmd = new usercmd_s
        {
            msec = 16, // 16ms frame
            viewangles = new Vector3(0, 90, 0),
            forwardmove = 400.0f,
            sidemove = 0.0f,
            upmove = 0.0f,
            buttons = 1, // IN_ATTACK
            impulse = 0,
            weaponselect = 1
        };
    }

    /// <summary>
    /// Example: Override HUD_UpdateClientData with proper types
    /// </summary>
    public override int HUD_UpdateClientData(client_data_t* cdata, float flTime)
    {
        if (cdata != null)
        {
            // Access client data fields safely
            var origin = cdata->origin;
            var viewangles = cdata->viewangles;
            var fov = cdata->fov;

            // Example: Modify FOV based on some condition
            if (fov < 90.0f)
            {
                cdata->fov = 90.0f;
            }

            // Example: Log player position
            Console.WriteLine($"Player at: {origin.X}, {origin.Y}, {origin.Z}");
        }

        return base.HUD_UpdateClientData(cdata, flTime);
    }

    /// <summary>
    /// Example: Override HUD_PlayerMove with proper types
    /// </summary>
    public override void HUD_PlayerMove(playermove_s* ppmove, int server)
    {
        if (ppmove != null)
        {
            // Access player movement data
            var origin = ppmove->origin;
            var velocity = ppmove->velocity;
            var onground = ppmove->onground;
            var waterlevel = ppmove->waterlevel;

            // Example: Custom movement modification
            if (waterlevel > 0)
            {
                // Reduce friction in water
                ppmove->friction *= 0.5f;
            }

            // Example: Anti-gravity cheat
            if (ppmove->cmd.impulse == 100)
            {
                ppmove->gravity = 0.1f;
            }
        }

        base.HUD_PlayerMove(ppmove, server);
    }

    /// <summary>
    /// Example: Override HUD_AddEntity with proper types
    /// </summary>
    public override int HUD_AddEntity(int type, cl_entity_s* ent, sbyte* modelname)
    {
        if (ent != null)
        {
            // Access entity data
            var origin = ent->origin;
            var angles = ent->angles;
            var index = ent->index;
            var isPlayer = ent->player.Value != 0;

            // Example: Hide certain entities
            if (type == 2 && !isPlayer) // Hide non-player entities of type 2
            {
                return 0; // Don't add to render list
            }

            // Example: Modify entity position
            if (isPlayer && index == 1) // Local player
            {
                // Add some screen shake effect
                ent->origin.Z += (float)(Math.Sin(Environment.TickCount * 0.01) * 2.0);
            }
        }

        return base.HUD_AddEntity(type, ent, modelname);
    }

    /// <summary>
    /// Example: Working with network addresses
    /// </summary>
    public void NetworkAddressExample()
    {
        var netAddr = new netadr_s
        {
            type = netadrtype_t.NA_IP,
            port = 27015
        };

        // Set IP address (127.0.0.1)
        netAddr.ip[0] = 127;
        netAddr.ip[1] = 0;
        netAddr.ip[2] = 0;
        netAddr.ip[3] = 1;
    }

    /// <summary>
    /// Example: Working with key buttons
    /// </summary>
    public void KeyButtonExample()
    {
        var button = new kbutton_s
        {
            state = 1 // Key is down
        };

        button.down[0] = 32; // Space key
        button.down[1] = 0;  // No second key
    }

    /// <summary>
    /// Example: Working with studio events
    /// </summary>
    public void StudioEventExample()
    {
        var studioEvent = new mstudioevent_s
        {
            frame = 10,
            @event = 1001, // Custom event ID
            type = 0
        };

        // Set options string (null-terminated)
        var optionStr = "sound weapons/rifle1.wav"u8;
        for (int i = 0; i < Math.Min(optionStr.Length, 63); i++)
        {
            studioEvent.options[i] = (sbyte)optionStr[i];
        }
        studioEvent.options[Math.Min(optionStr.Length, 63)] = 0; // Null terminator
    }

    /// <summary>
    /// Example: Working with trace results
    /// </summary>
    public void TraceResultExample()
    {
        var trace = new TraceResult
        {
            fAllSolid = new qboolean { Value = 0 },
            fStartSolid = new qboolean { Value = 0 },
            fInOpen = new qboolean { Value = 1 },
            fInWater = new qboolean { Value = 0 },
            flFraction = 1.0f, // Didn't hit anything
            vecEndPos = new Vector3(100, 200, 50),
            vecPlaneNormal = new Vector3(0, 0, 1), // Up
            iHitgroup = 0 // Generic
        };
    }

    /// <summary>
    /// Example: Working with Studio Model API
    /// </summary>
    public void StudioModelExample()
    {
        // Example of implementing HUD_GetStudioModelInterface
        // This is typically called by the engine to get our studio rendering interface
    }

    /// <summary>
    /// Example: Override HUD_GetStudioModelInterface with proper types
    /// </summary>
    public override int HUD_GetStudioModelInterface(int version, r_studio_interface_s** ppinterface, engine_studio_api_s* pstudio)
    {
        // Check version compatibility
        const int STUDIO_INTERFACE_VERSION = 1;
        if (version != STUDIO_INTERFACE_VERSION)
            return 0;

        // Create our studio interface
        var studioInterface = new r_studio_interface_s
        {
            version = STUDIO_INTERFACE_VERSION,
            StudioDrawModel = &MyStudioDrawModel,
            StudioDrawPlayer = &MyStudioDrawPlayer
        };

        // Set the interface pointer
        if (ppinterface != null)
        {
            // In a real implementation, you would allocate this properly
            // *ppinterface = &studioInterface;
        }

        // Copy engine studio functions for our use
        if (pstudio != null)
        {
            // Access engine studio API functions
            var modelLoader = pstudio->Mod_ForName;
            var currentEntity = pstudio->GetCurrentEntity;
            var memAlloc = pstudio->Mem_Calloc;

            // Example: Load a model
            // var model = modelLoader("models/player.mdl", 1);

            // Example: Get current rendering entity
            // var entity = currentEntity();

            // Example: Allocate memory
            // var buffer = memAlloc(1024, 1); // 1024 bytes
        }

        return 1; // Success
    }

    /// <summary>
    /// Example studio model drawing function
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int MyStudioDrawModel(int flags)
    {
        // Custom studio model rendering logic
        // This is where you would implement your custom model rendering

        // Example flags that might be passed:
        // STUDIO_RENDER = 1
        // STUDIO_EVENTS = 2
        // STUDIO_SHADOWDEPTH = 4

        // Return 1 for success, 0 for failure
        return 1;
    }

    /// <summary>
    /// Example studio player drawing function
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int MyStudioDrawPlayer(int flags, entity_state_s* pplayer)
    {
        // Custom player model rendering logic
        if (pplayer != null)
        {
            // Access player state
            var origin = pplayer->origin;
            var angles = pplayer->angles;
            var modelindex = pplayer->modelindex;
            var sequence = pplayer->sequence;
            var frame = pplayer->frame;

            // Example: Custom player rendering based on team
            if (pplayer->team == 1) // Terrorist team
            {
                // Apply red tint or special effects
            }
            else if (pplayer->team == 2) // Counter-terrorist team
            {
                // Apply blue tint or special effects
            }

            // Example: Custom animation blending
            var blending0 = pplayer->blending[0];
            var blending1 = pplayer->blending[1];

            // Example: Custom body/skin selection
            var body = pplayer->body;
            var skin = pplayer->skin;
        }

        return 1; // Success
    }

    /// <summary>
    /// Example: Working with engine studio API functions
    /// </summary>
    public void EngineStudioAPIExample(engine_studio_api_s* pstudio)
    {
        if (pstudio != null)
        {
            // Memory management
            var buffer = pstudio->Mem_Calloc(1024, 1); // Allocate 1024 bytes

            // Model management
            // Note: In real usage, you would need to convert strings to sbyte* properly
            var currentEntity = pstudio->GetCurrentEntity();

            // Rendering state
            if (pstudio->IsHardware() != 0)
            {
                // Hardware rendering (OpenGL/D3D)
                pstudio->SetupRenderer(0); // Setup for normal rendering

                // Custom rendering code here

                pstudio->RestoreRenderer(); // Restore state
            }
            else
            {
                // Software rendering
            }

            // Chrome effect
            if (pstudio->SetChromeOrigin != null)
            {
                pstudio->SetChromeOrigin();
            }

            // Player model setup
            if (pstudio->SetupPlayerModel != null)
            {
                var model = pstudio->SetupPlayerModel(1); // Player index 1
            }

            // Skin and texture setup
            if (pstudio->StudioSetupSkin != null)
            {
                pstudio->StudioSetupSkin(IntPtr.Zero, 0); // Default skin
            }

            // Color remapping (for team colors)
            if (pstudio->StudioSetRemapColors != null)
            {
                pstudio->StudioSetRemapColors(255, 0); // Red top, default bottom
            }

            // Debug rendering
            if (pstudio->StudioDrawBones != null)
            {
                pstudio->StudioDrawBones(); // Draw skeleton
            }

            if (pstudio->StudioDrawAbsBBox != null)
            {
                pstudio->StudioDrawAbsBBox(); // Draw bounding box
            }
        }
    }

    /// <summary>
    /// Example: Working with server studio API
    /// </summary>
    public void ServerStudioAPIExample(server_studio_api_s* pstudio)
    {
        if (pstudio != null)
        {
            // Server-side studio operations are more limited
            var buffer = pstudio->Mem_Calloc(512, 1);

            // Load model data
            var model = new model_s(); // Assume we have a model
            var extraData = pstudio->Mod_Extradata(&model);

            // Cache operations
            var cacheUser = new cache_user_s();
            var cachedData = pstudio->Cache_Check(&cacheUser);

            if (cachedData == IntPtr.Zero)
            {
                // Load from file if not in cache
                // Note: In real usage, you would need to convert strings to sbyte* properly
                // pstudio->LoadCacheFile(modelPath, &cacheUser);
            }
        }
    }

    /// <summary>
    /// Example: Working with server blending interface
    /// </summary>
    public void ServerBlendingExample(sv_blending_interface_s* pblending)
    {
        if (pblending != null && pblending->version == 1)
        {
            // Setup bones for server-side calculations
            var model = new model_s();
            float frame = 0.0f;
            int sequence = 0;
            Vector3 angles = new(0, 90, 0);
            Vector3 origin = new(100, 200, 50);

            byte[] controllers = new byte[4] { 0, 0, 0, 0 };
            byte[] blending = new byte[2] { 0, 0 };

            fixed (byte* pControllers = controllers)
            fixed (byte* pBlending = blending)
            {
                // This would typically be called for server-side bone calculations
                // Used for hit detection, attachment points, etc.
                pblending->SV_StudioSetupBones(&model, frame, sequence, &angles, &origin,
                    pControllers, pBlending, -1, null);
            }
        }
    }
}