﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Insthync.MMOG
{
    [RequireComponent(typeof(UIMmoServerEntrySelectionManager))]
    public class UIMmoServerList : UIBase
    {
        public UIMmoServerEntry uiServerEntryPrefab;
        public Transform uiServerEntryContainer;

        private UIList cacheList;
        public UIList CacheList
        {
            get
            {
                if (cacheList == null)
                {
                    cacheList = gameObject.AddComponent<UIList>();
                    cacheList.uiPrefab = uiServerEntryPrefab.gameObject;
                    cacheList.uiContainer = uiServerEntryContainer;
                }
                return cacheList;
            }
        }

        private UIMmoServerEntrySelectionManager selectionManager;
        public UIMmoServerEntrySelectionManager SelectionManager
        {
            get
            {
                if (selectionManager == null)
                    selectionManager = GetComponent<UIMmoServerEntrySelectionManager>();
                selectionManager.selectionMode = UISelectionMode.SelectSingle;
                return selectionManager;
            }
        }

        public void OnClickConnect()
        {
            var data = SelectionManager.SelectedUI.Data;
            MMOClientInstance.Singleton.StartCentralClient(data.networkAddress, data.networkPort);
        }

        public override void Show()
        {
            base.Show();
            SelectionManager.DeselectSelectedUI();
            SelectionManager.Clear();

            var networkSettings = MMOClientInstance.Singleton.networkSettings;
            CacheList.Generate(networkSettings, (index, networkSetting, ui) =>
            {
                var uiServerEntry = ui.GetComponent<UIMmoServerEntry>();
                uiServerEntry.Data = networkSetting;
                uiServerEntry.Show();
                SelectionManager.Add(uiServerEntry);
                if (index == 0)
                    uiServerEntry.OnClickSelect();
            });
        }
    }
}