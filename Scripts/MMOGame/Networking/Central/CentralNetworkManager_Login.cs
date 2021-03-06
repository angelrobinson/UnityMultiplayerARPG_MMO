﻿using LiteNetLib;
using LiteNetLibManager;
using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace MultiplayerARPG.MMO
{
    public partial class CentralNetworkManager
    {
        public uint RequestUserLogin(string username, string password, AckMessageCallback callback)
        {
            RequestUserLoginMessage message = new RequestUserLoginMessage();
            message.username = username;
            message.password = password;
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestUserLogin, message, callback);
        }

        public uint RequestUserRegister(string username, string password, AckMessageCallback callback)
        {
            RequestUserRegisterMessage message = new RequestUserRegisterMessage();
            message.username = username;
            message.password = password;
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestUserRegister, message, callback);
        }

        public uint RequestUserLogout(AckMessageCallback callback)
        {
            BaseAckMessage message = new BaseAckMessage();
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestUserLogout, message, callback);
        }

        public uint RequestValidateAccessToken(string userId, string accessToken, AckMessageCallback callback)
        {
            RequestValidateAccessTokenMessage message = new RequestValidateAccessTokenMessage();
            message.userId = userId;
            message.accessToken = accessToken;
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestValidateAccessToken, message, callback);
        }

        public uint RequestFacebookLogin(string id, string accessToken, AckMessageCallback callback)
        {
            RequestFacebookLoginMessage message = new RequestFacebookLoginMessage();
            message.id = id;
            message.accessToken = accessToken;
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestFacebookLogin, message, callback);
        }

        public uint RequestGooglePlayLogin(string idToken, AckMessageCallback callback)
        {
            RequestGooglePlayLoginMessage message = new RequestGooglePlayLoginMessage();
            message.idToken = idToken;
            return Client.ClientSendAckPacket(SendOptions.ReliableOrdered, MMOMessageTypes.RequestGooglePlayLogin, message, callback);
        }

        protected void HandleRequestUserLogin(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestUserLoginRoutine(messageHandler));
        }

        private IEnumerator HandleRequestUserLoginRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            RequestUserLoginMessage message = messageHandler.ReadMessage<RequestUserLoginMessage>();
            ResponseUserLoginMessage.Error error = ResponseUserLoginMessage.Error.None;
            ValidateUserLoginJob validateJob = new ValidateUserLoginJob(Database, message.username, message.password);
            validateJob.Start();
            yield return StartCoroutine(validateJob.WaitFor());
            string userId = validateJob.result;
            string accessToken = string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                error = ResponseUserLoginMessage.Error.InvalidUsernameOrPassword;
                userId = string.Empty;
            }
            else if (userPeersByUserId.ContainsKey(userId) || MapContainsUser(userId))
            {
                error = ResponseUserLoginMessage.Error.AlreadyLogin;
                userId = string.Empty;
            }
            else
            {
                CentralUserPeerInfo userPeerInfo = new CentralUserPeerInfo();
                userPeerInfo.connectionId = connectionId;
                userPeerInfo.userId = userId;
                userPeerInfo.accessToken = accessToken = Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "");
                userPeersByUserId[userId] = userPeerInfo;
                userPeers[connectionId] = userPeerInfo;
                UpdateAccessTokenJob updateAccessTokenJob = new UpdateAccessTokenJob(Database, userId, accessToken);
                updateAccessTokenJob.Start();
                yield return StartCoroutine(updateAccessTokenJob.WaitFor());
            }
            ResponseUserLoginMessage responseMessage = new ResponseUserLoginMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = error == ResponseUserLoginMessage.Error.None ? AckResponseCode.Success : AckResponseCode.Error;
            responseMessage.error = error;
            responseMessage.userId = userId;
            responseMessage.accessToken = accessToken;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseUserLogin, responseMessage);
        }

        protected void HandleRequestUserRegister(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestUserRegisterRoutine(messageHandler));
        }

        private IEnumerator HandleRequestUserRegisterRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            RequestUserRegisterMessage message = messageHandler.ReadMessage<RequestUserRegisterMessage>();
            ResponseUserRegisterMessage.Error error = ResponseUserRegisterMessage.Error.None;
            string username = message.username;
            string password = message.password;
            FindUsernameJob findUsernameJob = new FindUsernameJob(Database, username);
            findUsernameJob.Start();
            yield return StartCoroutine(findUsernameJob.WaitFor());
            if (findUsernameJob.result > 0)
                error = ResponseUserRegisterMessage.Error.UsernameAlreadyExisted;
            else if (string.IsNullOrEmpty(username) || username.Length < minUsernameLength)
                error = ResponseUserRegisterMessage.Error.TooShortUsername;
            else if (username.Length > maxUsernameLength)
                error = ResponseUserRegisterMessage.Error.TooLongUsername;
            else if (string.IsNullOrEmpty(password) || password.Length < minPasswordLength)
                error = ResponseUserRegisterMessage.Error.TooShortPassword;
            else
            {
                CreateUserLoginJob createUserLoginJob = new CreateUserLoginJob(Database, username, password);
                createUserLoginJob.Start();
                yield return StartCoroutine(createUserLoginJob.WaitFor());
            }
            ResponseUserRegisterMessage responseMessage = new ResponseUserRegisterMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = error == ResponseUserRegisterMessage.Error.None ? AckResponseCode.Success : AckResponseCode.Error;
            responseMessage.error = error;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseUserRegister, responseMessage);
        }

        protected void HandleRequestUserLogout(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestUserLogoutRoutine(messageHandler));
        }

        private IEnumerator HandleRequestUserLogoutRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            BaseAckMessage message = messageHandler.ReadMessage<BaseAckMessage>();
            CentralUserPeerInfo userPeerInfo;
            if (userPeers.TryGetValue(connectionId, out userPeerInfo))
            {
                userPeersByUserId.Remove(userPeerInfo.userId);
                userPeers.Remove(connectionId);
                UpdateAccessTokenJob updateAccessTokenJob = new UpdateAccessTokenJob(Database, userPeerInfo.userId, string.Empty);
                updateAccessTokenJob.Start();
                yield return StartCoroutine(updateAccessTokenJob.WaitFor());
            }
            BaseAckMessage responseMessage = new BaseAckMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = AckResponseCode.Success;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseUserLogout, responseMessage);
        }

        protected void HandleRequestValidateAccessToken(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestValidateAccessTokenRoutine(messageHandler));
        }

        private IEnumerator HandleRequestValidateAccessTokenRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            RequestValidateAccessTokenMessage message = messageHandler.ReadMessage<RequestValidateAccessTokenMessage>();
            ResponseValidateAccessTokenMessage.Error error = ResponseValidateAccessTokenMessage.Error.None;
            string userId = message.userId;
            string accessToken = message.accessToken;
            ValidateAccessTokenJob validateAccessTokenJob = new ValidateAccessTokenJob(Database, userId, accessToken);
            validateAccessTokenJob.Start();
            yield return StartCoroutine(validateAccessTokenJob.WaitFor());
            if (!validateAccessTokenJob.result)
            {
                error = ResponseValidateAccessTokenMessage.Error.InvalidAccessToken;
                userId = string.Empty;
                accessToken = string.Empty;
            }
            else
            {
                CentralUserPeerInfo userPeerInfo;
                if (userPeersByUserId.TryGetValue(userId, out userPeerInfo))
                {
                    userPeersByUserId.Remove(userPeerInfo.userId);
                    userPeers.Remove(userPeerInfo.connectionId);
                }
                userPeerInfo = new CentralUserPeerInfo();
                userPeerInfo.connectionId = connectionId;
                userPeerInfo.userId = userId;
                userPeerInfo.accessToken = accessToken = Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "");
                userPeersByUserId[userId] = userPeerInfo;
                userPeers[connectionId] = userPeerInfo;
                UpdateAccessTokenJob updateAccessTokenJob = new UpdateAccessTokenJob(Database, userId, accessToken);
                updateAccessTokenJob.Start();
                yield return StartCoroutine(updateAccessTokenJob.WaitFor());
            }
            ResponseValidateAccessTokenMessage responseMessage = new ResponseValidateAccessTokenMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = error == ResponseValidateAccessTokenMessage.Error.None ? AckResponseCode.Success : AckResponseCode.Error;
            responseMessage.error = error;
            responseMessage.userId = userId;
            responseMessage.accessToken = accessToken;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseValidateAccessToken, responseMessage);
        }

        protected void HandleRequestFacebookLogin(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestFacebookLoginRoutine(messageHandler));
        }

        private IEnumerator HandleRequestFacebookLoginRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            RequestFacebookLoginMessage message = messageHandler.ReadMessage<RequestFacebookLoginMessage>();
            ResponseUserLoginMessage.Error error = ResponseUserLoginMessage.Error.None;
            FacebookLoginJob job = new FacebookLoginJob(Database, message.id, message.accessToken);
            job.Start();
            yield return StartCoroutine(job.WaitFor());
            string userId = job.result;
            string accessToken = string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                error = ResponseUserLoginMessage.Error.InvalidUsernameOrPassword;
                userId = string.Empty;
            }
            else if (userPeersByUserId.ContainsKey(userId) || MapContainsUser(userId))
            {
                error = ResponseUserLoginMessage.Error.AlreadyLogin;
                userId = string.Empty;
            }
            else
            {
                CentralUserPeerInfo userPeerInfo = new CentralUserPeerInfo();
                userPeerInfo.connectionId = connectionId;
                userPeerInfo.userId = userId;
                userPeerInfo.accessToken = accessToken = Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "");
                userPeersByUserId[userId] = userPeerInfo;
                userPeers[connectionId] = userPeerInfo;
                UpdateAccessTokenJob updateAccessTokenJob = new UpdateAccessTokenJob(Database, userId, accessToken);
                updateAccessTokenJob.Start();
                yield return StartCoroutine(updateAccessTokenJob.WaitFor());
            }
            ResponseUserLoginMessage responseMessage = new ResponseUserLoginMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = error == ResponseUserLoginMessage.Error.None ? AckResponseCode.Success : AckResponseCode.Error;
            responseMessage.error = error;
            responseMessage.userId = userId;
            responseMessage.accessToken = accessToken;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseUserLogin, responseMessage);
        }

        protected void HandleRequestGooglePlayLogin(LiteNetLibMessageHandler messageHandler)
        {
            StartCoroutine(HandleRequestGooglePlayLoginRoutine(messageHandler));
        }

        IEnumerator HandleRequestGooglePlayLoginRoutine(LiteNetLibMessageHandler messageHandler)
        {
            long connectionId = messageHandler.connectionId;
            RequestGooglePlayLoginMessage message = messageHandler.ReadMessage<RequestGooglePlayLoginMessage>();
            ResponseUserLoginMessage.Error error = ResponseUserLoginMessage.Error.None;
            GooglePlayLoginJob job = new GooglePlayLoginJob(Database, message.idToken);
            job.Start();
            yield return StartCoroutine(job.WaitFor());
            string userId = job.result;
            string accessToken = string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                error = ResponseUserLoginMessage.Error.InvalidUsernameOrPassword;
                userId = string.Empty;
            }
            else if (userPeersByUserId.ContainsKey(userId) || MapContainsUser(userId))
            {
                error = ResponseUserLoginMessage.Error.AlreadyLogin;
                userId = string.Empty;
            }
            else
            {
                CentralUserPeerInfo userPeerInfo = new CentralUserPeerInfo();
                userPeerInfo.connectionId = connectionId;
                userPeerInfo.userId = userId;
                userPeerInfo.accessToken = accessToken = Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "");
                userPeersByUserId[userId] = userPeerInfo;
                userPeers[connectionId] = userPeerInfo;
                UpdateAccessTokenJob updateAccessTokenJob = new UpdateAccessTokenJob(Database, userId, accessToken);
                updateAccessTokenJob.Start();
                yield return StartCoroutine(updateAccessTokenJob.WaitFor());
            }
            ResponseUserLoginMessage responseMessage = new ResponseUserLoginMessage();
            responseMessage.ackId = message.ackId;
            responseMessage.responseCode = error == ResponseUserLoginMessage.Error.None ? AckResponseCode.Success : AckResponseCode.Error;
            responseMessage.error = error;
            responseMessage.userId = userId;
            responseMessage.accessToken = accessToken;
            ServerSendPacket(connectionId, SendOptions.ReliableOrdered, MMOMessageTypes.ResponseUserLogin, responseMessage);
        }

        protected void HandleResponseUserLogin(LiteNetLibMessageHandler messageHandler)
        {
            TransportHandler transportHandler = messageHandler.transportHandler;
            ResponseUserLoginMessage message = messageHandler.ReadMessage<ResponseUserLoginMessage>();
            uint ackId = message.ackId;
            transportHandler.TriggerAck(ackId, message.responseCode, message);
        }

        protected void HandleResponseUserRegister(LiteNetLibMessageHandler messageHandler)
        {
            TransportHandler transportHandler = messageHandler.transportHandler;
            ResponseUserRegisterMessage message = messageHandler.ReadMessage<ResponseUserRegisterMessage>();
            uint ackId = message.ackId;
            transportHandler.TriggerAck(ackId, message.responseCode, message);
        }

        protected void HandleResponseUserLogout(LiteNetLibMessageHandler messageHandler)
        {
            TransportHandler transportHandler = messageHandler.transportHandler;
            BaseAckMessage message = messageHandler.ReadMessage<BaseAckMessage>();
            uint ackId = message.ackId;
            transportHandler.TriggerAck(ackId, message.responseCode, message);
        }

        protected void HandleResponseValidateAccessToken(LiteNetLibMessageHandler messageHandler)
        {
            TransportHandler transportHandler = messageHandler.transportHandler;
            ResponseValidateAccessTokenMessage message = messageHandler.ReadMessage<ResponseValidateAccessTokenMessage>();
            uint ackId = message.ackId;
            transportHandler.TriggerAck(ackId, message.responseCode, message);
        }
    }
}
