using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Radio
{
    public class WalkieTalkieLn : MonoBehaviour
    {
        private PhysGrabObject? physGrabObject;
        private Light? lightDotBroadcasting;
        public Light? light;
        private Coroutine? lightBlinkRoutine;



        private TextMeshProUGUI? mytextMeshUI;

        public bool equiped { get; private set; } = false;

        private GameObject? broadCastToGameObject;
        private static int firstChannel;

        private bool autoSetupDone = false;

        private TextMeshPro? textSource;
        private TextMeshPro? textDestination;
        private TextMeshPro? textBroadcastSource;

        private WalkieTalkieLn? broadcastToWalkieTalkieScript;

        public ItemEquippable? itemEquipableScript;

        public bool broadcastedToThisInstance = false;
        public int currentChannelInstance { get; private set; } = 1;
        public int toChannelInstance { get; private set; } = 1;
        public int broadcastFromInstance = 1;

        private PhotonView? photonView;

        private static Dictionary<int, GameObject> _allRadios = new Dictionary<int, GameObject>();

        private bool lastEquiped = true;
        private GameObject? latestOwnersRadioGameObject;
        private int switches = 2;
        private int lastChannel = 0;
        private bool secondTry = false;
        private bool isTalking;
        private PlayerAvatar? latestOwnerLocalInstance;

        private void Awake()
        {
            RegisterWalkeiTalkieInstance();
            if (gameObject.name == "0InstanceWalkie") return;
            DisplayChannel();

            physGrabObject = this.gameObject.GetComponent<PhysGrabObject>();
            light = gameObject.transform.GetChild(0).gameObject.GetComponent<Light>();
            lightDotBroadcasting = gameObject.transform.GetChild(3).gameObject.GetComponent<Light>();
            itemEquipableScript = this.gameObject.GetComponent<ItemEquippable>();
        }
        private void Update()
        {
            if (gameObject.name == "0InstanceWalkie") return;

            if (physGrabObject == null || light == null || lightDotBroadcasting == null || itemEquipableScript == null) return;

            if (itemEquipableScript.currentState == ItemEquippable.ItemState.Equipped || itemEquipableScript.currentState == ItemEquippable.ItemState.Equipping)
            {
                equiped = true;
            }
            else
            {
                equiped = false;
            }

            if (lastEquiped != equiped)
            {
                try
                {
                    if (equiped)
                    {
                        if (broadcastFromInstance < 1) return;

                        if (_allRadios.TryGetValue(broadcastFromInstance, out var forTryFind))
                        {
                            if (forTryFind.GetComponent<WalkieTalkieLn>() == null)
                            {
                                Debug.LogError($"no script in equiped on id {broadcastFromInstance}; {forTryFind.name} ");
                                return;
                            }
                            forTryFind.GetComponent<WalkieTalkieLn>().latestOwnersRadioGameObject = this.gameObject;
                            forTryFind.GetComponent<WalkieTalkieLn>().TargetInInventory(currentChannelInstance);
                            var findOwnerGameObject = PhotonView.Find(itemEquipableScript.ownerPlayerId);
                            if (findOwnerGameObject != null)
                            {
                                latestOwnerLocalInstance = findOwnerGameObject.gameObject.GetComponent<PlayerAvatar>();
                                if (latestOwnerLocalInstance == null)
                                {
                                    Debug.LogError($"no Equipable component on /player/gameObject {findOwnerGameObject}");
                                }
                            }
                            else
                            {
                                Debug.LogError($"no object/player with id {itemEquipableScript.ownerPlayerId}");
                            }


                        }

                    }
                    else
                    {
                        latestOwnerLocalInstance = null;
                    }

                    lastEquiped = equiped;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }


            if (physGrabObject.grabbed)
            {
                if (!autoSetupDone)
                {
                    if (firstChannel != currentChannelInstance)
                    {
                        photonView.RPC("SetChannel", RpcTarget.All, firstChannel);
                    }
                    else
                    {
                        SwitchChannel();
                    }

                    Debug.Log("SETTED UP A RADIO -> " + gameObject.name + "channel from: " + currentChannelInstance + "; channel to: " + toChannelInstance + "; ");
                    DisplayChannel();
                    autoSetupDone = true;
                }

                if (lightBlinkRoutine == null)
                    lightBlinkRoutine = StartCoroutine(BlinkRoutine());

                if (Input.GetKeyDown(KeyCode.V))
                {
                    SwitchChannel();
                }
                BroadCast();

            }
            else
            {
                if (mytextMeshUI != null && !string.IsNullOrEmpty(mytextMeshUI.text))
                {
                    HudDeactivate();
                }
                if (lightBlinkRoutine != null)
                {
                    StopCoroutine(lightBlinkRoutine);
                    lightBlinkRoutine = null;
                }
            }



            if (broadcastedToThisInstance)
            {
                if (itemEquipableScript.currentState != ItemEquippable.ItemState.Equipped) return;

                if (latestOwnerLocalInstance == null) return;

                if (latestOwnerLocalInstance.playerName.Equals(PlayerAvatar.instance.playerName) == false) return;

                if (mytextMeshUI == null)
                {
                    HudGenerate();
                }
                else if (!mytextMeshUI.IsActive())
                {
                    HudActivate();
                }
            }
            else
            {
                if (mytextMeshUI != null && !string.IsNullOrEmpty(mytextMeshUI.text))
                {
                    HudDeactivate();
                }
            }

        }

        private void TargetInInventory(int currentCh)
        {

        }

        [PunRPC]
        private void HudActivate()
        {
            mytextMeshUI.gameObject.SetActive(true);
            mytextMeshUI.SetText("INCOMING MESSAGE<br>take your walkie talkie from inventory");
        }
        [PunRPC]
        private void HudDeactivate()
        {
            mytextMeshUI.gameObject.SetActive(false);
            mytextMeshUI.SetText("");
        }
        [PunRPC]
        private void HudGenerate()
        {
            GameObject hudObj = new GameObject("RadioMessage", typeof(RectTransform));
            mytextMeshUI = hudObj.AddComponent<TextMeshProUGUI>();
            var existingHealthUI = HealthUI.instance.GetComponent<TextMeshProUGUI>();
            var myRectTransform = mytextMeshUI.rectTransform;
            myRectTransform.anchorMin = new Vector2(1f, 0.5f);
            myRectTransform.anchorMax = new Vector2(1f, 0.5f);
            myRectTransform.pivot = new Vector2(0f, 1f);
            mytextMeshUI.transform.SetParent(existingHealthUI.transform.parent, false);
            mytextMeshUI.font = existingHealthUI.font;
            mytextMeshUI.material = existingHealthUI.fontMaterial;
            mytextMeshUI.color = Color.red;
            mytextMeshUI.fontSize = existingHealthUI.fontSize - 12.5f;
            mytextMeshUI.rectTransform.anchoredPosition = new Vector2(-180, 0);
            mytextMeshUI.SetText("INCOMING MESSAGE<br>take your walkie talkie from inventory");
            mytextMeshUI.enabled = true;
        }


        void BroadCast()
        {
            try
            {
                Vector3 physGrabTargetObjectPosition = Vector3.zero;

                if (toChannelInstance == currentChannelInstance)
                {
                    SwitchChannel();
                    return;
                }

                if (broadCastToGameObject == null)
                {
                    broadCastToGameObject = tryGetGameObject();
                }
                else if (broadCastToGameObject != latestOwnersRadioGameObject)
                {
                    latestOwnersRadioGameObject = broadCastToGameObject;
                }

                if (broadCastToGameObject == null)
                {
                    Debug.LogError("still no gameobject");
                    return;
                }
                else if (broadcastToWalkieTalkieScript == null)
                {
                    broadcastToWalkieTalkieScript = broadCastToGameObject.GetComponent<WalkieTalkieLn>();
                }

                if (broadcastToWalkieTalkieScript == null)
                {
                    Debug.LogError("Still no script");
                    return;
                }

                if (broadcastToWalkieTalkieScript.broadcastFromInstance != currentChannelInstance)
                {
                    broadcastToWalkieTalkieScript.broadcastFromInstance = currentChannelInstance;
                    broadcastToWalkieTalkieScript.photonView.RPC("SetFromBroadcast", RpcTarget.Others, currentChannelInstance);
                }

                float intensityRatio = 0f;
                if (physGrabObject == null) return;

                isTalking = false;

                // Loop through grabbed players
                foreach (PhysGrabber item in physGrabObject.playerGrabbing)
                {
                    var voice = item.playerAvatar.voiceChat;
                    if (voice == null || !item.playerAvatar.voiceChatFetched)
                    {
                        Debug.LogError($"[VOICE SKIP] voice null or not fetched. playerAvatar: {item.playerAvatar}");
                        continue;
                    }

                    if (!broadcastToWalkieTalkieScript || !broadcastToWalkieTalkieScript.photonView)
                    {
                        Debug.LogError("[VOICE SKIP] WalkieTalkie script or photonView missing");
                        continue;
                    }

                    if (physGrabTargetObjectPosition == Vector3.zero)
                    {

                        var tmpPhysGrab = broadCastToGameObject.GetComponent<PhysGrabObject>();

                        if (tmpPhysGrab != null && tmpPhysGrab)
                        {
                            physGrabTargetObjectPosition = tmpPhysGrab.centerPoint;
                        }
                        else if (broadCastToGameObject)
                        {
                            physGrabTargetObjectPosition = broadCastToGameObject.transform.position;
                        }
                        else
                        {
                            Debug.LogError("[POSITION FAIL] broadCastToGameObject is NULL, cannot resolve position");
                            return;
                        }
                    }

                    if (physGrabTargetObjectPosition == new Vector3(0, 3000, 0))
                    {

                        if (latestOwnersRadioGameObject == null)
                        {
                            Debug.LogError("[POSITION FAIL] latestOwnersRadioGameObject is NULL but target is (0,3000,0)");
                            return;
                        }
                    }

                    voice.OverridePosition(physGrabTargetObjectPosition, 0.2f);
                    voice.OverridePitch(0.85f, 0.1f, 0.1f, 0.2f, 0.05f, 100f);
                    item.playerAvatar.voiceChat.OverrideNoTalkAnimation(0.2f);
                    isTalking = true;
                    if (voice.clipLoudness > 0.005f)
                    {
                        intensityRatio += voice.clipLoudness * 5;
                    }
                }

                if (isTalking && !broadcastToWalkieTalkieScript.broadcastedToThisInstance)
                {
                    broadcastToWalkieTalkieScript.photonView.RPC("SetCurrentlyBroadcasting", RpcTarget.All, true);
                }
                if (!isTalking && broadcastToWalkieTalkieScript.broadcastedToThisInstance)
                {
                    broadcastToWalkieTalkieScript.photonView.RPC("SetCurrentlyBroadcasting", RpcTarget.All, false);
                }

                Light? targetLight = null;
                if (broadCastToGameObject && broadCastToGameObject.transform.childCount > 0)
                    targetLight = broadCastToGameObject.transform.GetChild(0).GetComponent<Light>();
                if (targetLight != null) targetLight.intensity = intensityRatio;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private GameObject? tryGetGameObject()
        {
            Vector3 physGrabTargetObjectPosition = Vector3.zero;
            GameObject? radioObject = null;

            // Find or update broadcast target

            if (_allRadios.Count <= 1) { Debug.LogError("_allradios is empty or 1"); return null; }

            if (_allRadios.TryGetValue(toChannelInstance, out radioObject))
            {
                if (radioObject != null)
                {
                    lastChannel = toChannelInstance;
                    photonView.RPC("SetChannel", RpcTarget.Others, toChannelInstance);
                    return radioObject;
                }
            }


            var orderedChannels = _allRadios.Keys.OrderBy(x => x).ToList();

            if (!secondTry)
            {
                foreach (var ch in orderedChannels)
                {
                    if (ch == toChannelInstance) continue;

                    _allRadios.TryGetValue(ch, out var forTryFind);
                    if (forTryFind == null) continue;

                    toChannelInstance = ch;
                    lastChannel = ch;
                    return forTryFind;
                }
                secondTry = true;
                return null;
            }

            return broadCastToGameObject;
        }

        private void GiveUp()
        {
            toChannelInstance = 0;
            photonView.RPC("SetChannel", RpcTarget.Others, toChannelInstance);
        }

        private void SwitchChannel()
        {
            var orderedChannels = _allRadios.Keys.OrderBy(x => x).ToList();
            if (orderedChannels.Count < 2) return;
            int index = orderedChannels.IndexOf(toChannelInstance);
            toChannelInstance = orderedChannels[(index + 1) % orderedChannels.Count];
            photonView.RPC("SetChannel", RpcTarget.Others, toChannelInstance);
            DisplayChannel();
            photonView.RPC("DisplayNet", RpcTarget.Others);
        }
        [PunRPC]
        private void DisplayNet()
        {
            DisplayChannel();
        }

        private void ReAsignRadios()
        {
            throw new Exception("No radios at all Antony did shit his pants, please contact him asap on discord -> lnxoid");
        }
        private void RegisterWalkeiTalkieInstance()
        {
            try
            {
                light = gameObject.transform.GetChild(0).GetComponent<Light>();
                if (light == null) { gameObject.name = "0InstanceWalkie"; return; }
            }
            catch
            {
                gameObject.name = "0InstanceWalkie";
                return;
            }

            photonView = GetComponent<PhotonView>();

            currentChannelInstance = photonView.ViewID;
            gameObject.name = "WalkieTalkie" + currentChannelInstance;
            _allRadios.Add(currentChannelInstance, gameObject);
            if (_allRadios.Count == 1)
            {
                firstChannel = currentChannelInstance;
                photonView.RPC("SetFirstChannel", RpcTarget.Others, currentChannelInstance);
            }

            Debug.Log(_allRadios.Count + " < count; " + gameObject.name);
        }

        [PunRPC]
        void SetChannel(int newChannelId)
        {
            toChannelInstance = newChannelId;
            DisplayChannel();
        }
        [PunRPC]
        void SetFirstChannel(int newChannelId)
        {
            firstChannel = newChannelId;
            DisplayChannel();
        }
        [PunRPC]
        void SetFromBroadcast(int fromBroadcastId)
        {
            broadcastFromInstance = fromBroadcastId;
            DisplayChannel();
        }
        [PunRPC]
        void SetCurrentlyBroadcasting(bool currentlyBroadcasting)
        {
            broadcastedToThisInstance = currentlyBroadcasting;
            DisplayChannel();
        }
        [PunRPC]
        void SetEquiped(bool newEquiped)
        {
            equiped = newEquiped;
        }
        [PunRPC]
        void SetBroadcastGameObject(int viewId)
        {
            if (viewId == 0)
            {
                broadCastToGameObject = null;
                broadcastToWalkieTalkieScript = null;
                return;
            }

            var viewIdFind = PhotonView.Find(viewId);
            if (viewIdFind == null)
            {
                Debug.LogError($"Gameobject with id {viewId} is null");
                return;
            }
            broadCastToGameObject = viewIdFind.gameObject;

            broadcastToWalkieTalkieScript = broadCastToGameObject.GetComponent<WalkieTalkieLn>();
        }

        private void DisplayChannel()
        {
            textSource = gameObject.transform.GetChild(1).gameObject.GetComponent<TextMeshPro>();
            textDestination = gameObject.transform.GetChild(2).gameObject.GetComponent<TextMeshPro>();
            textBroadcastSource = gameObject.transform.GetChild(4).gameObject.GetComponent<TextMeshPro>();

            textBroadcastSource.text = "from " + (broadcastFromInstance % 100).ToString();
            textSource.text = (currentChannelInstance % 100).ToString() + "";
            textDestination.text = (toChannelInstance % 100).ToString() + "";
        }
        IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (lightDotBroadcasting == null)
                    yield break;

                lightDotBroadcasting.intensity =
                    lightDotBroadcasting.intensity > 0.01f ? 0f : 0.05f;

                yield return new WaitForSeconds(1f);
            }
        }
        void OnDestroy()
        {
            _allRadios.Remove(this.currentChannelInstance);
        }
    }
}