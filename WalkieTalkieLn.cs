using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Radio
{
    public class WalkieTalkieLn : MonoBehaviour
    {
        private static int firstChannel;
        private static Dictionary<int, GameObject> _allRadios = new Dictionary<int, GameObject>();

        [SerializeField] private Sound? messageIncomeSound;
        [SerializeField] private Sound? changeOrEndChannelSound;
        [SerializeField] private Sound? noiseLoopSound;

        private bool findWalkieTalkieOutOfListComplete = false;

        private PhysGrabObject? physGrabObject;
        public Light? lightMain;

        private TextMeshProUGUI? warningTextMeshGUI;

        public bool isThisEquiped { get; private set; } = false;

        private GameObject? destintionGameObject;

        private bool autoSetupDone = false;

        [SerializeField] private TextMeshPro? textSource;
        [SerializeField] private TextMeshPro? textDestination;
        [SerializeField] private TextMeshPro? textBroadcastFromSource;

        private WalkieTalkieLn? destinationGameObjectScript;

        public ItemEquippable? itemEquipableScript;

        public bool isReceiving = false;
        public int currentChannelSource { get; private set; } = 1;
        public int toChannelDestination { get; private set; } = 1;
        public int fromChannelReceiving = 1;

        private PhotonView? photonView;

        private bool isEquipedLately = true;
        private int latestChannelDestination = 1;
        private bool isCurrentlyBroadcastingTo;

        private GameObject? latestOwnersWalkieGameObject;
        private PlayerAvatar? latestOwnerAvatar;

        private bool isReceivedLately;

        // I mean i'm soryy for that shit but it works so well you can't even imagine
        private Vector3 globalPositionInInventory = new Vector3(0, 3000, 0);

        private void Awake()
        {
            RegisterWalkeiTalkieInstance();
            if (gameObject.name.Equals("0InstanceWalkie")) return;
            DisplayChannel();

            physGrabObject = this.gameObject.GetComponent<PhysGrabObject>();
            lightMain = gameObject.transform.GetChild(0).gameObject.GetComponent<Light>();
            itemEquipableScript = this.gameObject.GetComponent<ItemEquippable>();
        }
        private void FixedUpdate()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            if (physGrabObject == null) return;

            List<PhysGrabber> playerGrabbing = physGrabObject.playerGrabbing;
            bool flag = false;
            foreach (PhysGrabber item in playerGrabbing)
            {
                if (item.isRotating)
                {
                    flag = true;
                }
            }
            if (!flag)
            {
                Quaternion turnX = Quaternion.Euler(0f, -180f, 0f);
                Quaternion turnY = Quaternion.Euler(0f, 0f, 0f);
                Quaternion identity = Quaternion.identity;
                physGrabObject.TurnXYZ(turnX, turnY, identity);
                physGrabObject.OverrideTorqueStrength(2f + physGrabObject.massOriginal);
            }
        }
        private void Update()
        {
            if (gameObject.name.Equals("0InstanceWalkie")) return;

            if (physGrabObject == null || lightMain == null || itemEquipableScript == null) return;

            isThisEquiped = (itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped || itemEquipableScript.currentState == ItemEquippable.ItemState.Equipping);

            UpdateEquiping();

            if (physGrabObject.grabbed)
            {
                AutoSetup();

                if (SemiFunc.InputDown(BindConfig.switchWalkieChannel.inputKey))
                {
                    photonView.RPC("SwitchChannel", RpcTarget.All); ;

                    photonView.RPC("PlayOutSound", RpcTarget.All);
                }

                BroadCast();

            }
            else if (destinationGameObjectScript != null && destinationGameObjectScript.fromChannelReceiving == currentChannelSource)
            {
                if (destinationGameObjectScript.warningTextMeshGUI != null && !string.IsNullOrEmpty(destinationGameObjectScript.warningTextMeshGUI.text))
                {
                    if (destinationGameObjectScript.isReceiving)
                    {
                        destinationGameObjectScript.isReceiving = false;
                        destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.Others, false);

                        destinationGameObjectScript.photonView.RPC("HudDeactivate", RpcTarget.All);
                    }
                }
            }
            if (itemEquipableScript != null && itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped && destinationGameObjectScript != null && destinationGameObjectScript.fromChannelReceiving == currentChannelSource)
            {
                if (destinationGameObjectScript.isReceiving)
                {
                    destinationGameObjectScript.isReceiving = false;
                    destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.Others, false);
                    destinationGameObjectScript.photonView.RPC("PlayOutSound", RpcTarget.All);
                    destinationGameObjectScript.photonView.RPC("HudDeactivate", RpcTarget.All);
                }
            }

            if(fromChannelReceiving == 0)
            {
                isReceiving = false;
                photonView.RPC("SetIsReceiving", RpcTarget.Others, false);
            }


            if (isReceiving != isReceivedLately)
            {
                photonView.RPC("PlayLoopBackgroundSound", RpcTarget.All, isReceiving);
                isReceivedLately = isReceiving;
            }
            else
            {
                return;
            }

            if (isReceiving)
            {
                if (itemEquipableScript.currentState != ItemEquippable.ItemState.Equipped) { return; }

                if (latestOwnerAvatar == null) return;

                if (latestOwnerAvatar.playerName.Equals(PlayerAvatar.instance.playerName) == false) return;

                if (warningTextMeshGUI == null)
                {
                    HudGenerate();
                }
                else if (!warningTextMeshGUI.IsActive())
                {
                    HudActivate();
                }
            }
            else
            {
                if (warningTextMeshGUI != null && !string.IsNullOrEmpty(warningTextMeshGUI.text))
                {
                    HudDeactivate();
                }
            }

        }

        private void AutoSetup()
        {
            if (!autoSetupDone)
            {
                if (firstChannel != currentChannelSource)
                {
                    photonView.RPC("SetChannel", RpcTarget.All, firstChannel);
                }
                else
                {
                    photonView.RPC("SwitchChannel", RpcTarget.All); ;
                }
                DisplayChannel();
                autoSetupDone = true;
            }
        }

        private void UpdateEquiping()
        {
            if (isEquipedLately == isThisEquiped) return;

            if (isThisEquiped)
            {
                if (fromChannelReceiving < 1) return;

                latestOwnerAvatar = getOwnerAvatar(fromChannelReceiving);
            }
            else
            {
                latestOwnerAvatar = null;
            }

            isEquipedLately = isThisEquiped;
        }

        private PlayerAvatar? getOwnerAvatar(int channelId)
        {
            if (channelId == currentChannelSource) return PlayerAvatar.instance;

            if (!_allRadios.TryGetValue(channelId, out var forTryFind)) return null;

            if (forTryFind.GetComponent<WalkieTalkieLn>() == null)
            {
                return null;
            }
            forTryFind.GetComponent<WalkieTalkieLn>().latestOwnersWalkieGameObject = this.gameObject;
            var findOwnerGameObject = PhotonView.Find(itemEquipableScript.ownerPlayerId);
            if (findOwnerGameObject != null)
            {
                latestOwnerAvatar = findOwnerGameObject.gameObject.GetComponent<PlayerAvatar>();

                if (latestOwnerAvatar == null)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
            return latestOwnerAvatar;
        }

        [PunRPC]
        private void HudActivate()
        {
            if (warningTextMeshGUI == null) HudGenerate();
            warningTextMeshGUI.gameObject.SetActive(true);
            warningTextMeshGUI.SetText("INCOMING MESSAGE");
        }
        [PunRPC]
        private void HudDeactivate()
        {
            if (warningTextMeshGUI == null) return;
            warningTextMeshGUI.gameObject.SetActive(false);
            warningTextMeshGUI.SetText("");
        }
        [PunRPC]
        private void HudGenerate()
        {
            GameObject hudObj = new GameObject("RadioMessage", typeof(RectTransform));
            warningTextMeshGUI = hudObj.AddComponent<TextMeshProUGUI>();
            var existingHealthUI = HealthUI.instance.GetComponent<TextMeshProUGUI>();
            var warningsRectTransform = warningTextMeshGUI.rectTransform;
            warningsRectTransform.anchorMin = new Vector2(0.5f, 0f);
            warningsRectTransform.anchorMax = new Vector2(0.5f, 0f);
            warningsRectTransform.pivot = new Vector2(0f, 1f);
            warningTextMeshGUI.transform.SetParent(existingHealthUI.transform.parent, false);
            warningTextMeshGUI.font = existingHealthUI.font;
            warningTextMeshGUI.material = existingHealthUI.fontMaterial;
            warningTextMeshGUI.color = Color.red;
            warningTextMeshGUI.fontSize = existingHealthUI.fontSize - 12.5f;
            warningTextMeshGUI.rectTransform.anchoredPosition = new Vector2(-60, 115);
            warningTextMeshGUI.SetText("INCOMING MESSAGE");
            warningTextMeshGUI.enabled = true;
        }

        void BroadCast()
        {
            isCurrentlyBroadcastingTo = false;
            Vector3 DestinationWalkiePosition = Vector3.zero;

            if (toChannelDestination == 0)
            {
                return;
            }

            if (toChannelDestination == currentChannelSource)
            {
                photonView.RPC("SwitchChannel", RpcTarget.All); ;
                return;
            }

            if (destintionGameObject == null)
            {
                destintionGameObject = tryGetDestinationWalkieGameObject();
            }

            if (destintionGameObject == null)
            {
                return;
            }
            else if (destinationGameObjectScript == null)
            {
                destinationGameObjectScript = destintionGameObject.GetComponent<WalkieTalkieLn>();
            }

            if (destinationGameObjectScript == null)
            {
                return;
            }

            if (destinationGameObjectScript.fromChannelReceiving != currentChannelSource)
            {
                destinationGameObjectScript.fromChannelReceiving = currentChannelSource;
                destinationGameObjectScript.photonView.RPC("SetFromChannelReceiving", RpcTarget.Others, currentChannelSource);              
            }

            float intensityRatio = 0f;
            if (physGrabObject == null) return;

            foreach (PhysGrabber item in physGrabObject.playerGrabbing)
            {
                var voice = item.playerAvatar.voiceChat;
                if (!item.playerAvatar.voiceChatFetched)
                    continue;

                bool succes = getDestinationPosition(ref DestinationWalkiePosition);

                if (!succes) return;

                //ACTUAL CODE THAT DOES WALKIE TALKIES;) \/ taken from original valuableTalkingBotHead.cs and modifies
                voice.OverridePosition(DestinationWalkiePosition, 0.2f);
                voice.OverridePitch(0.85f, 0.1f, 0.1f, 0.2f, 0.05f, 100f);
                item.playerAvatar.voiceChat.OverrideNoTalkAnimation(0.2f);
                isCurrentlyBroadcastingTo = true;
                if (voice.clipLoudness > 0.005f)
                {
                    intensityRatio += voice.clipLoudness * 8;
                }
            }

            if (destinationGameObjectScript == null) return;

            if (isCurrentlyBroadcastingTo && !destinationGameObjectScript.isReceiving)
            {
                destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.All, true);

                destinationGameObjectScript.photonView.RPC("PlayIncomeSound", RpcTarget.All);
            }
            if (!isCurrentlyBroadcastingTo && destinationGameObjectScript.isReceiving)
            {
                destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.All, false);

                destinationGameObjectScript.photonView.RPC("PlayOutSound", RpcTarget.All);
            }

            destinationGameObjectScript.lightMain.intensity = intensityRatio;

        }

        private bool getDestinationPosition(ref Vector3 destinationObjectPosition)
        {
            if (destintionGameObject == null) return false;

            if (destinationObjectPosition == Vector3.zero)
            {
                var tmpPhysGrab = destintionGameObject.GetComponent<PhysGrabObject>();

                if (tmpPhysGrab != null && tmpPhysGrab)
                {
                    destinationObjectPosition = tmpPhysGrab.centerPoint;
                }
                else if (destintionGameObject)
                {
                    destinationObjectPosition = destintionGameObject.transform.position;
                }
                else
                {
                    return false;
                }
            }

            if (destinationObjectPosition == globalPositionInInventory)
            {
                if (latestOwnersWalkieGameObject == null)
                {
                    return false;
                }
            }
            return true;
        }

        private GameObject? tryGetDestinationWalkieGameObject()
        {
            Vector3 physGrabTargetObjectPosition = Vector3.zero;
            GameObject? radioObject = null;

            if (_allRadios.Count <= 1) { return null; }

            if (_allRadios.TryGetValue(toChannelDestination, out radioObject))
            {
                if (radioObject != null)
                {
                    photonView.RPC("SetChannel", RpcTarget.Others, toChannelDestination);
                    return radioObject;
                }
            }


            var orderedChannels = _allRadios.Keys.OrderBy(x => x).ToList();

            if (!findWalkieTalkieOutOfListComplete)
            {
                foreach (var ch in orderedChannels)
                {
                    if (ch == toChannelDestination) continue;

                    _allRadios.TryGetValue(ch, out var forTryFind);
                    if (forTryFind == null) continue;

                    toChannelDestination = ch;
                    return forTryFind;
                }
                findWalkieTalkieOutOfListComplete = true;
                return null;
            }

            return destintionGameObject;
        }

        private void GiveUp()
        {
            toChannelDestination = 0;
            photonView.RPC("SetChannel", RpcTarget.Others, toChannelDestination);
        }
        [PunRPC]
        private void SwitchChannel()
        {
            var orderedChannels = _allRadios.Keys.OrderBy(x => x).ToList();
            if (orderedChannels.Count < 2) return;
            int index = orderedChannels.IndexOf(toChannelDestination);
            toChannelDestination = orderedChannels[(index + 1) % orderedChannels.Count];

            if (destintionGameObject != null && destinationGameObjectScript != null)
            {
                destinationGameObjectScript.photonView.RPC("SetFromChannelReceiving", RpcTarget.All, 0);
            }
            destintionGameObject = null;
            destinationGameObjectScript = null;

            DisplayChannel();
        }
        [PunRPC]
        private void DisplayNet()
        {
            DisplayChannel();
        }

        [PunRPC]
        void SetChannel(int newChannelId)
        {
            toChannelDestination = newChannelId;
            DisplayChannel();
        }
        [PunRPC]
        void SetFirstChannel(int newChannelId)
        {
            firstChannel = newChannelId;
            DisplayChannel();
        }
        [PunRPC]
        void SetFromChannelReceiving(int fromBroadcastId)
        {
            fromChannelReceiving = fromBroadcastId;
            DisplayChannel();
        }
        [PunRPC]
        void SetIsReceiving(bool currentlyBroadcasting)
        {
            isReceiving = currentlyBroadcasting;
            DisplayChannel();
        }
        [PunRPC]
        void SetEquiped(bool newEquiped)
        {
            isThisEquiped = newEquiped;
        }

        [PunRPC]
        void PlayIncomeSound()
        {
            bool succes = PlayInInventory(ref messageIncomeSound);

            if (!succes)
                messageIncomeSound.Play(gameObject.transform.position);
        }

        [PunRPC]
        void PlayOutSound()
        {
            bool succes = PlayInInventory(ref changeOrEndChannelSound);

            if (!succes)
                changeOrEndChannelSound.Play(gameObject.transform.position);
        }

        [PunRPC]
        void PlayLoopBackgroundSound(bool toPlay)
        {
            noiseLoopSound.PlayLoop(toPlay, 10f, 10f);
        }

        private bool PlayInInventory(ref Sound targetSoundInstance)
        {
            if (itemEquipableScript != null)
            {
                if (itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped)
                {
                    var avatar = getOwnerAvatar(currentChannelSource);

                    if (avatar == null) return false;

                    var audioSource = avatar.gameObject.GetComponent<AudioSource>();
                    // Not sure if this is a good idea but it works
                    if (audioSource == null)
                    {
                        audioSource = avatar.gameObject.AddComponent<AudioSource>();
                        avatar.gameObject.AddComponent<AudioLowPassFilter>();
                        avatar.gameObject.AddComponent<AudioLowPassLogic>();
                    }
                    var tmpSource = targetSoundInstance.Source;

                    targetSoundInstance.Source = audioSource;
                    targetSoundInstance.Play(avatar.clientPositionCurrent);

                    targetSoundInstance.Source = tmpSource;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private void DisplayChannel()
        {

            //hardocoded thing maybe Find would look better but i don't know about productivity and uhhh
            if (textSource == null)
                textSource = gameObject.transform.GetChild(1).gameObject.GetComponent<TextMeshPro>();

            if (textDestination == null)
                textDestination = gameObject.transform.GetChild(2).gameObject.GetComponent<TextMeshPro>();

            if (textBroadcastFromSource == null)
                textBroadcastFromSource = gameObject.transform.GetChild(4).gameObject.GetComponent<TextMeshPro>();

            // It's about limiting displaying id's that have more than 2 digits also hardcoded thing
            textBroadcastFromSource.SetText("from " + (fromChannelReceiving % 100).ToString());
            textSource.SetText((currentChannelSource % 100).ToString() );
            textDestination.SetText((toChannelDestination % 100).ToString() );
        }

        private void RegisterWalkeiTalkieInstance()
        {
            try
            {
                lightMain = gameObject.transform.GetChild(0).GetComponent<Light>();
                if (lightMain == null) { gameObject.name = "0InstanceWalkie"; return; }
            }
            catch
            {
                gameObject.name = "0InstanceWalkie";
                return;
            }

            photonView = GetComponent<PhotonView>();

            currentChannelSource = photonView.ViewID;
            gameObject.name = "WalkieTalkie" + currentChannelSource;
            _allRadios.Add(currentChannelSource, gameObject);
            if (_allRadios.Count == 1)
            {
                firstChannel = currentChannelSource;
                photonView.RPC("SetFirstChannel", RpcTarget.Others, currentChannelSource);
            }
        }

        private void Start()
        {
            if (_allRadios.Count < 2) return;

            if (firstChannel != currentChannelSource)
            {
                photonView.RPC("SetChannel", RpcTarget.All, firstChannel);
            }
            else
            {
                photonView.RPC("SetChannel", RpcTarget.All, currentChannelSource);
                photonView.RPC("SwitchChannel", RpcTarget.All);
                photonView.RPC("DisplayNet", RpcTarget.All);
            }
        }

        void OnDestroy()
        {
            _allRadios.Remove(this.currentChannelSource);
        }
    }
}