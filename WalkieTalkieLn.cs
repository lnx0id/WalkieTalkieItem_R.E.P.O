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

        private bool findWalkieTalkieOutOfListComplete = false;

        private PhysGrabObject? physGrabObject;
        public Light? lightMain;

        private TextMeshProUGUI? warningTextMeshGUI;

        public bool isThisEquiped { get; private set; } = false;

        private GameObject? destintionGameObject;

        private bool autoSetupDone = false;

        private TextMeshPro? textSource;
        private TextMeshPro? textDestination;
        private TextMeshPro? textBroadcastFromSource;

        private WalkieTalkieLn? destinationGameObjectScript;

        public ItemEquippable? itemEquipableScript;

        public bool isReceiving = false;
        public int currentChannelSource { get; private set; } = 1;
        public int toChannelDestination { get; private set; } = 1;
        public int fromChannelReceiving = 1;

        private PhotonView? photonView;

        private bool lastlyEquiped = true;
        private int latestChannelDestination = 0;
        private bool isCurrentlyBroadcastingTo;

        private GameObject? latestOwnersWalkieGameObject;
        private PlayerAvatar? latestOwnerAvatar;
        private Vector3 globalPositionInInventory = new Vector3(0, 3000, 0);

        private void Awake()
        {
            RegisterWalkeiTalkieInstance();
            if (gameObject.name == "0InstanceWalkie") return;
            DisplayChannel();

            physGrabObject = this.gameObject.GetComponent<PhysGrabObject>();
            lightMain = gameObject.transform.GetChild(0).gameObject.GetComponent<Light>();
            itemEquipableScript = this.gameObject.GetComponent<ItemEquippable>();
        }
        private void Update()
        {
            if (gameObject.name == "0InstanceWalkie") return;

            if (physGrabObject == null || lightMain == null || itemEquipableScript == null) return;

            isThisEquiped = (itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped || itemEquipableScript.currentState == ItemEquippable.ItemState.Equipping);

            UpdateEquiping();

            if (physGrabObject.grabbed)
            {
                AutoSetup();

                if (Input.GetKeyDown(KeyCode.V))
                {
                    SwitchChannel();

                    changeOrEndChannelSound.Play(gameObject.transform.position);
                }

                BroadCast();

            }
            else if (destinationGameObjectScript != null && destinationGameObjectScript.fromChannelReceiving == currentChannelSource)
            {
                if (destinationGameObjectScript.warningTextMeshGUI != null && !string.IsNullOrEmpty(destinationGameObjectScript.warningTextMeshGUI.text))
                {
                    destinationGameObjectScript.isReceiving = false;
                    destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.Others, false);

                    destinationGameObjectScript.photonView.RPC("HudDeactivate", RpcTarget.All);
                }
            }
            if (itemEquipableScript != null && itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped && destinationGameObjectScript != null && destinationGameObjectScript.fromChannelReceiving == currentChannelSource)
            {
                destinationGameObjectScript.isReceiving = false;
                destinationGameObjectScript.photonView.RPC("SetIsReceiving", RpcTarget.Others, false);

                destinationGameObjectScript.photonView.RPC("HudDeactivate", RpcTarget.All);
            }



            if (isReceiving)
            {
                if (itemEquipableScript.currentState != ItemEquippable.ItemState.Equipped) return;

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
                    SwitchChannel();
                }

                Debug.Log("SETTED UP A RADIO -> " + gameObject.name + "channel from: " + currentChannelSource + "; channel to: " + toChannelDestination + "; ");
                DisplayChannel();
                autoSetupDone = true;
            }
        }

        private void UpdateEquiping()
        {
            if (lastlyEquiped == isThisEquiped) return;

            if (isThisEquiped)
            {
                if (fromChannelReceiving < 1) return;

                latestOwnerAvatar = getOwnerAvatar(fromChannelReceiving);
            }
            else
            {
                latestOwnerAvatar = null;
            }

            lastlyEquiped = isThisEquiped;
        }

        private PlayerAvatar? getOwnerAvatar(int channelId)
        {
            if (channelId == currentChannelSource) return PlayerAvatar.instance;

            if (!_allRadios.TryGetValue(channelId, out var forTryFind)) return null;

            if (forTryFind.GetComponent<WalkieTalkieLn>() == null)
            {
                Debug.LogError($"no script in equiped on id {channelId}; {forTryFind.name} ");
                return null;
            }
            forTryFind.GetComponent<WalkieTalkieLn>().latestOwnersWalkieGameObject = this.gameObject;
            var findOwnerGameObject = PhotonView.Find(itemEquipableScript.ownerPlayerId);
            if (findOwnerGameObject != null)
            {
                latestOwnerAvatar = findOwnerGameObject.gameObject.GetComponent<PlayerAvatar>();

                if (latestOwnerAvatar == null)
                {
                    Debug.LogError($"no Equipable component on /player/gameObject {findOwnerGameObject}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"no object/player with id {itemEquipableScript.ownerPlayerId}");
                return null;
            }
            return latestOwnerAvatar;
        }

        [PunRPC]
        private void HudActivate()
        {
            if (warningTextMeshGUI == null) HudGenerate();
            warningTextMeshGUI.gameObject.SetActive(true);
            warningTextMeshGUI.SetText("INCOMING MESSAGE<br>take your walkie talkie from inventory");
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
            var myRectTransform = warningTextMeshGUI.rectTransform;
            myRectTransform.anchorMin = new Vector2(1f, 0.5f);
            myRectTransform.anchorMax = new Vector2(1f, 0.5f);
            myRectTransform.pivot = new Vector2(0f, 1f);
            warningTextMeshGUI.transform.SetParent(existingHealthUI.transform.parent, false);
            warningTextMeshGUI.font = existingHealthUI.font;
            warningTextMeshGUI.material = existingHealthUI.fontMaterial;
            warningTextMeshGUI.color = Color.red;
            warningTextMeshGUI.fontSize = existingHealthUI.fontSize - 12.5f;
            warningTextMeshGUI.rectTransform.anchoredPosition = new Vector2(-180, 0);
            warningTextMeshGUI.SetText("INCOMING MESSAGE<br>take your walkie talkie from inventory");
            warningTextMeshGUI.enabled = true;
        }


        void BroadCast()
        {
            try
            {
                Vector3 DestinationWalkiePosition = Vector3.zero;

                if (toChannelDestination == currentChannelSource)
                {
                    SwitchChannel();
                    return;
                }

                if (destintionGameObject == null)
                {
                    destintionGameObject = tryGetGameObject();
                }
                else if (destintionGameObject != latestOwnersWalkieGameObject)
                {
                    latestOwnersWalkieGameObject = destintionGameObject;
                }

                if (destintionGameObject == null)
                {
                    Debug.LogError("still no gameobject");
                    return;
                }
                else if (destinationGameObjectScript == null)
                {
                    destinationGameObjectScript = destintionGameObject.GetComponent<WalkieTalkieLn>();
                }

                if (destinationGameObjectScript == null)
                {
                    Debug.LogError("Still no script");
                    return;
                }

                if (destinationGameObjectScript.fromChannelReceiving != currentChannelSource)
                {
                    destinationGameObjectScript.fromChannelReceiving = currentChannelSource;
                    destinationGameObjectScript.photonView.RPC("SetFromChannelReceiving", RpcTarget.Others, currentChannelSource);
                }

                float intensityRatio = 0f;
                if (physGrabObject == null) return;

                isCurrentlyBroadcastingTo = false;

                foreach (PhysGrabber item in physGrabObject.playerGrabbing)
                {
                    var voice = item.playerAvatar.voiceChat;
                    if (voice == null || !item.playerAvatar.voiceChatFetched)
                    {
                        Debug.LogError($"[VOICE SKIP] voice null or not fetched. playerAvatar: {item.playerAvatar}");
                        continue;
                    }

                    bool succes = getDestinationPosition(ref DestinationWalkiePosition);


                    //ACTUAL CODE THAT DOES WALKIE TALKIES;) \/
                    voice.OverridePosition(DestinationWalkiePosition, 0.2f);
                    voice.OverridePitch(0.85f, 0.1f, 0.1f, 0.2f, 0.05f, 100f);
                    item.playerAvatar.voiceChat.OverrideNoTalkAnimation(0.2f);
                    isCurrentlyBroadcastingTo = true;
                    if (voice.clipLoudness > 0.005f)
                    {
                        intensityRatio += voice.clipLoudness * 8;
                    }
                }
                if (destintionGameObject == null) return;
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
            catch (Exception e)
            {
                Debug.LogException(e);
            }
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
                    Debug.LogError("[POSITION FAIL] broadCastToGameObject is NULL, cannot resolve position");
                    return false;
                }
            }

            if (destinationObjectPosition == globalPositionInInventory)
            {
                if (latestOwnersWalkieGameObject == null)
                {
                    Debug.LogError("[POSITION FAIL] latestOwnersRadioGameObject is NULL but target is (0,3000,0)");
                    return false;
                }
            }
            return true;
        }

        private GameObject? tryGetGameObject()
        {
            Vector3 physGrabTargetObjectPosition = Vector3.zero;
            GameObject? radioObject = null;

            if (_allRadios.Count <= 1) { Debug.LogError("_allradios is empty or 1"); return null; }

            if (_allRadios.TryGetValue(toChannelDestination, out radioObject))
            {
                if (radioObject != null)
                {
                    latestChannelDestination = toChannelDestination;
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
                    latestChannelDestination = ch;
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

        private void SwitchChannel()
        {
            var orderedChannels = _allRadios.Keys.OrderBy(x => x).ToList();
            if (orderedChannels.Count < 2) return;
            int index = orderedChannels.IndexOf(toChannelDestination);
            toChannelDestination = orderedChannels[(index + 1) % orderedChannels.Count];
            photonView.RPC("SetChannel", RpcTarget.Others, toChannelDestination);
            DisplayChannel();
            photonView.RPC("DisplayNet", RpcTarget.Others);
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
        void SetDestinationGameObject(int viewId)
        {
            if (viewId == 0)
            {
                destintionGameObject = null;
                destinationGameObjectScript = null;
                return;
            }

            var viewIdFind = PhotonView.Find(viewId);
            if (viewIdFind == null)
            {
                Debug.LogError($"Gameobject with id {viewId} is null");
                return;
            }
            destintionGameObject = viewIdFind.gameObject;

            destinationGameObjectScript = destintionGameObject.GetComponent<WalkieTalkieLn>();
        }

        [PunRPC]
        void PlayIncomeSound()
        {
            if (itemEquipableScript != null)
            {
                if (itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped)
                {
                    var ownerAvatartmp = getOwnerAvatar(currentChannelSource);

                    if (ownerAvatartmp == null) { Debug.Log("sound cant be played no player avatar"); }
                    else { messageIncomeSound.Play(ownerAvatartmp.transform.parent.position ); return; }
                }
            }

            messageIncomeSound.Play(gameObject.transform.position);
        }
        [PunRPC]
        void PlayOutSound()
        {
            changeOrEndChannelSound.Play(gameObject.transform.position);
        }

        private void DisplayChannel()
        {

            //hardocoded thing maybe Find would look better
            if (textSource == null)
                textSource = gameObject.transform.GetChild(1).gameObject.GetComponent<TextMeshPro>();

            if (textDestination == null)
                textDestination = gameObject.transform.GetChild(2).gameObject.GetComponent<TextMeshPro>();

            if (textBroadcastFromSource == null)
                textBroadcastFromSource = gameObject.transform.GetChild(4).gameObject.GetComponent<TextMeshPro>();

            // It's about limiting displaying id's that have more than 2 digits
            textBroadcastFromSource.SetText("from " + (fromChannelReceiving % 100).ToString());
            textSource.SetText((currentChannelSource % 100).ToString() + "");
            textDestination.SetText((toChannelDestination % 100).ToString() + "");
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

            Debug.Log(_allRadios.Count + " < count; " + gameObject.name);
        }

        void OnDestroy()
        {
            _allRadios.Remove(this.currentChannelSource);
        }
    }
}