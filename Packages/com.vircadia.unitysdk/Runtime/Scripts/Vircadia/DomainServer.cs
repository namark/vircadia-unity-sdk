//
//  DomainServer.cs
//  Runtime/Scripts/Vircadia
//
//  Created by Nshan G. on 21 Mar 2022.
//  Copyright 2022 Vircadia contributors.
//  Copyright 2022 DigiSomni LLC.
//
//  Distributed under the Apache License, Version 2.0.
//  See the accompanying file LICENSE or http://www.apache.org/licenses/LICENSE-2.0.html
//

using System;
using System.Runtime.InteropServices;

namespace Vircadia
{

    /// <summary>
    /// Represents the type of a node.
    /// </summary>
    public enum NodeType : byte
    {
        /// <summary>
        /// Domain server (Vircadia.DomainServer).
        /// </summary>
        DomainServer = (byte)'D',

        /// <summary>
        /// Entity server.
        /// </summary>
        EntityServer = (byte)'o',

        /// <summary>
        /// Agent.
        /// </summary>
        Agent = (byte)'I',

        /// <summary>
        /// Audio mixer (Vircadia.DomainServer.Audio).
        /// </summary>
        AudioMixer = (byte)'M',

        /// <summary>
        /// Avatar mixer (Vircadia.DomainServer.Avatar).
        /// </summary>
        AvatarMixer = (byte)'W',

        /// <summary>
        /// Asset server.
        /// </summary>
        AssetServer = (byte)'A',

        /// <summary>
        /// Message mixer (Vircadia.DomainServer.Messages).
        /// </summary>
        MessagesMixer = (byte)'m',

        /// <summary>
        /// Entity script server.
        /// </summary>
        EntityScriptServer = (byte)'S',

        /// <summary>
        /// Upstream audio mixer.
        /// </summary>
        UpstreamAudioMixer = (byte)'B',

        /// <summary>
        /// Upstream avatar mixer.
        /// </summary>
        UpstreamAvatarMixer = (byte)'C',

        /// <summary>
        /// Downstream audio mixer.
        /// </summary>
        DownstreamAudioMixer = (byte)'a',

        /// <summary>
        /// Downstream avatar mixer.
        /// </summary>
        DownstreamAvatarMixer = (byte)'w',

        /// <summary>
        /// Unassigned.
        /// </summary>
        Unassigned = 1
    }

    /// <summary>
    /// Optional fixed ports data.
    /// </summary>
    [System.Serializable]
    public struct Ports
    {
        /// <summary>
        /// UDP port for the client to listen on. (default: random port)
        /// </summary>
        public int? listenPort;

        /// <summary>
        /// DTLS port for the client to listen on. (default: not used)
        /// </summary>
        public int? dtlsListenPort;
    }

    /// <summary>
    /// Node (assignment client) data.
    /// </summary>
    public struct Node
    {
        /// <summary>
        /// Type of the node.
        /// </summary>
        public NodeType type;

        /// <summary>
        /// Indicated weather the node is active, or not.
        /// </summary>
        public bool active;

        /// <summary>
        /// A human readable address/port representation.
        /// </summary>
        public string address;

        /// <summary>
        /// A universal unique ID of the node (the string representation
        /// might not match between implementations)
        /// </summary>
        public Guid uuid;
    }

    /// <summary>
    /// Possible states of the <see
    /// cref="Vircadia.DomainServer"> DomainServer </see>
    /// class.
    /// </summary>
    public enum DomainServerStatus
    {
        /// <summary>
        /// Successfully Connected to a domain server, specified with
        /// <see cref="Vircadia.DomainServer.Connect"> Connect
        /// </see> method.
        /// </summary>
        Connected,

        /// <summary>
        /// Not yet connected to any domain server.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Connection or initialization error.
        /// TODO: Finer grained errors
        /// </summary>
        Error,

        /// <summary>
        /// Native API reported connection status that this
        /// implementation does not recognize.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Initializes the client and provides an API for domain server
    /// connection.
    /// </summary>
    public class DomainServer
    {

        /// <summary>
        /// Initializes the client and provides an API for domain server
        /// connection.
        /// </summary>
        /// <param name="ports"> Optional fixed ports to use. </param>
        /// <param name="platformInfo"> Optional platform infomration.
        /// TODO: clarify the format. </param>
        /// <param name="userAgent"> User agent string to use when
        /// connecting to Metaverse server (default: platform specific).
        /// </param>
        /// <param name="appName"> Client application name, used for
        /// settings and log files (default: VircadiaClient). </param>
        /// <param name="appOrganization"> Client application
        /// organization name, used for settings and log file directory
        /// (default: Vircadia). </param>
        /// <param name="appDomain"> Client application domain name.
        /// (default: vircadia.com). TODO: figure out what this is for.
        /// </param>
        /// <param name="appVersion"> Client application version.
        /// (default: native API version). </param>
        public DomainServer
        (
            Ports? ports = null,
            string platformInfo = null,
            string userAgent = null,
            string appName = null,
            string appOrganization = null,
            string appDomain = null,
            string appVersion = null
        )
        {
            var parameters = VircadiaNative.Context.vircadia_context_defaults();
            if (ports != null)
            {
                if (ports.Value.listenPort != null)
                {
                    parameters.listenPort = ports.Value.listenPort.Value;
                }

                if (ports.Value.dtlsListenPort != null)
                {
                    parameters.dtlsListenPort = ports.Value.dtlsListenPort.Value;
                }
            }

            Utils.CreateUnmanaged(ref parameters.platform_info, platformInfo);
            Utils.CreateUnmanaged(ref parameters.user_agent, userAgent);

            this._context = VircadiaNative.Context.vircadia_create_context(parameters);

            Utils.DestroyUnmanaged(parameters.platform_info, platformInfo);
            Utils.DestroyUnmanaged(parameters.user_agent, userAgent);

            Messages = this._context >= 0 ? new MessagesClient(this) : null;
            Avatar = this._context >= 0 ? new AvatarManager(this) : null;
            Audio = this._context >= 0 ? new AudioClient(this) : null;
        }

        /// <summary>
        /// Cleans up all the resources allocated for this client. This is
        /// called in the finalizer, but need to be called explictly in
        /// situations where finalizers can't be relied upon (specifically the
        /// Unity editor and unit tests).
        /// </summary>
        public void Destroy()
        {
            if (this._context < 0)
            {
                return;
            }

            if (VircadiaNative.Context.vircadia_destroy_context(this._context) < 0)
            {
                // TODO: report error somehow
            }
        }

        ~DomainServer()
        {
            Destroy();
        }

        /// <summary>
        /// Connect or jump to specified location.
        /// </summary>
        /// <param name="location">The address to go to: a "hifi://"
        /// address, an IP address (e.g., "127.0.0.1" or "localhost"), a
        /// file:/// address, a domain name, a named path on a domain
        /// (starts with "/"), a position or position and orientation,
        /// or a user (starts with "@").</param>
        public void Connect(string location)
        {
            IntPtr locationPtr = IntPtr.Zero;
            Utils.CreateUnmanaged(ref locationPtr, location);
            VircadiaNative.NodeList.vircadia_connect(this._context, locationPtr);
            Utils.DestroyUnmanaged(locationPtr, location);
        }

        /// <summary>
        /// The current status.
        /// </summary>
        public DomainServerStatus Status
        {
            get
            {
                if (this._context < 0)
                {
                    return DomainServerStatus.Error;
                }

                int nativeStatus = VircadiaNative.NodeList.vircadia_connection_status(this._context);
                if (nativeStatus < 0)
                {
                    return DomainServerStatus.Error;
                }
                else if (nativeStatus == 0)
                {
                    return DomainServerStatus.Disconnected;
                }
                else if (nativeStatus == 1)
                {
                    return DomainServerStatus.Connected;
                }

                return DomainServerStatus.Unknown;
            }
        }

        /// <summary>
        /// A list of connected nodes (assignment clients).
        /// </summary>
        public Node[] Nodes
        {
            get
            {
                if(VircadiaNative.NodeList.vircadia_update_nodes(this._context) < 0)
                {
                    return null;
                }

                int count = VircadiaNative.NodeList.vircadia_node_count(this._context);

                if (count < 0)
                {
                    return null;
                }

                Node[] values = new Node[count];

                byte[] uuidBytes = new byte[16];
                for (int i = 0; i < count ; ++i)
                {
                    var uuid = Utils.getUUID(VircadiaNative.NodeList.vircadia_node_uuid(this._context, i));
                    int active = VircadiaNative.NodeList.vircadia_node_active(this._context, i);
                    int type = VircadiaNative.NodeList.vircadia_node_type(this._context, i);

                    if (uuid == null || active < 0 || type < 0)
                    {
                        return null;
                    }

                    values[i].uuid = uuid.Value;
                    values[i].active = active == 1;
                    values[i].type = (NodeType)type;

                    // TODO fill other node fields
                }

                return values;
            }
        }

        /// <summary>
        /// This client's unique identifier.
        /// </summary>
        // TOOD: this requires Nodes getter, since update call is there, that's confusing should probably make it explicit
        public Guid? sessionUUID
        {
            get
            {
                return Utils.getUUID(VircadiaNative.NodeList.vircadia_client_get_session_uuid(_context));
            }
        }

        /// <summary>
        /// Accessor for the messaging functionality of the client.
        /// </summary>
        public MessagesClient Messages { get; private set; }

        /// <summary>
        /// Accessor for the avatars functionality of the client.
        /// </summary>
        public AvatarManager Avatar { get; private set; }

        /// <summary>
        /// Accessor for the audio functionality of the client.
        /// </summary>
        public AudioClient Audio { get; private set; }

        internal int ContextId {
            get { return _context; }
        }

        private int _context;

    }
}
