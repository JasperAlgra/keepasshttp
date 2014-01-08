﻿using KeePassLib;
using KeePassHttp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KeePassLib.Collections;

namespace KeePassHttp
{
    public partial class OptionsForm : Form
    {
        readonly ConfigOpt _config;
        private bool _restartRequired = false;

        public OptionsForm(ConfigOpt config)
        {
            _config = config;
            InitializeComponent();

            // Find all groups and add to comboBox.
            // Display the group names + add Uuid values to item
            PwDatabase db = KeePass.Program.MainForm.DocumentManager.ActiveDatabase;
            foreach (PwGroup thisGroup in db.RootGroup.GetGroups(true)) RootGroup.Items.Add(new ComboBoxItem(thisGroup.Name, thisGroup.Uuid.ToHexString()));
        }


        private PwEntry GetConfigEntry(PwDatabase db)
        {
            var kphe = new KeePassHttpExt();
            var root = db.RootGroup;

            // Get stored RootGroup from config if any
            if (_config.RootGroup != "")
            {
                var GroupUuid = new PwUuid(StringToByteArray(_config.RootGroupUuid));
                root = root.FindGroup(GroupUuid, true);
            }
            
            var uuid = new PwUuid(kphe.KEEPASSHTTP_UUID);
            var entry = root.FindEntry(uuid, false);
            return entry;
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private void OptionsForm_Load(object sender, EventArgs e)
        {
            credNotifyCheckbox.Checked = _config.ReceiveCredentialNotification;
            credMatchingCheckbox.Checked = _config.SpecificMatchingOnly;
            unlockDatabaseCheckbox.Checked = _config.UnlockDatabaseRequest;
            credAllowAccessCheckbox.Checked = _config.AlwaysAllowAccess;
            credAllowUpdatesCheckbox.Checked = _config.AlwaysAllowUpdates;
            credSearchInAllOpenedDatabases.Checked = _config.SearchInAllOpenedDatabases;
            matchSchemesCheckbox.Checked = _config.MatchSchemes;
            returnStringFieldsCheckbox.Checked = _config.ReturnStringFields;
            SortByUsernameRadioButton.Checked = _config.SortResultByUsername;
            SortByTitleRadioButton.Checked = !_config.SortResultByUsername;
            portNumber.Value = _config.ListenerPort;
            //RootGroup.Text = _config.RootGroup;
            //RootGroup.Text = _config.RootGroupUuid;
            RootGroup.Items.Add(new ComboBoxItem(_config.RootGroup, _config.RootGroupUuid));
            RootGroup.SelectedIndex = RootGroup.Items.Count -1 ;    // Select latest
        }


        private void okButton_Click(object sender, EventArgs e)
        {
            _config.ReceiveCredentialNotification = credNotifyCheckbox.Checked;
            _config.SpecificMatchingOnly = credMatchingCheckbox.Checked;
            _config.UnlockDatabaseRequest = unlockDatabaseCheckbox.Checked;
            _config.AlwaysAllowAccess = credAllowAccessCheckbox.Checked;
            _config.AlwaysAllowUpdates = credAllowUpdatesCheckbox.Checked;
            _config.SearchInAllOpenedDatabases = credSearchInAllOpenedDatabases.Checked;
            _config.MatchSchemes = matchSchemesCheckbox.Checked;
            _config.ReturnStringFields = returnStringFieldsCheckbox.Checked;
            _config.SortResultByUsername = SortByUsernameRadioButton.Checked;
            _config.ListenerPort = (int)portNumber.Value;
            _config.RootGroup = RootGroup.Text;
            _config.RootGroupUuid = ((ComboBoxItem)RootGroup.SelectedItem).HiddenValue;

            if (_restartRequired)
            {
                MessageBox.Show(
                    "You have successfully changed the port number.\nA restart of KeePass is required!\n\nPlease restart KeePass now.",
                    "Restart required!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            if (KeePass.Program.MainForm.DocumentManager.ActiveDatabase.IsOpen)
            {
                PwDatabase db = KeePass.Program.MainForm.DocumentManager.ActiveDatabase;
                var entry = GetConfigEntry(db);
                if (entry != null)
                {
                    List<string> deleteKeys = new List<string>();

                    foreach (var s in entry.Strings)
                    {
                        if (s.Key.IndexOf(KeePassHttpExt.ASSOCIATE_KEY_PREFIX) == 0)
                        {
                            deleteKeys.Add(s.Key);
                        }
                    }


                    if(deleteKeys.Count > 0)
                    {
                        PwObjectList<PwEntry> m_vHistory = entry.History.CloneDeep();
                        entry.History = m_vHistory;
                        entry.CreateBackup(null);

                        foreach (var key in deleteKeys)
                        {
                            entry.Strings.Remove(key);
                        }

                        entry.Touch(true);
                        KeePass.Program.MainForm.UpdateUI(false, null, true, db.RootGroup, true, null, true);
                        MessageBox.Show(
                            String.Format("Successfully removed {0} encryption-key{1} from KeePassHttp Settings.", deleteKeys.Count.ToString(), deleteKeys.Count == 1 ? "" : "s"),
                            String.Format("Removed {0} key{1} from database", deleteKeys.Count.ToString(), deleteKeys.Count == 1 ? "" : "s"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    else
                    {
                        MessageBox.Show(
                            "No shared encryption-keys found in KeePassHttp Settings.", "No keys found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                }
                else
                {
                    MessageBox.Show("The active database does not contain an entry of KeePassHttp Settings.", "KeePassHttp Settings not available!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("The active database is locked!\nPlease unlock the selected database or choose another one which is unlocked.", "Database locked!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void removePermissionsButton_Click(object sender, EventArgs e)
        {
            if (KeePass.Program.MainForm.DocumentManager.ActiveDatabase.IsOpen)
            {
                PwDatabase db = KeePass.Program.MainForm.DocumentManager.ActiveDatabase;

                uint counter = 0;
                var entries = db.RootGroup.GetEntries(true);

                if (entries.Count() > 999)
                {
                    MessageBox.Show(
                        String.Format("{0} entries detected!\nSearching and removing permissions could take some while.\n\nWe will inform you when the process has been finished.", entries.Count().ToString()),
                        String.Format("{0} entries detected", entries.Count().ToString()),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }

                foreach (var entry in entries) {
                    foreach (var str in entry.Strings)
                    {
                        if (str.Key == KeePassHttpExt.KEEPASSHTTP_NAME)
                        {
                            PwObjectList<PwEntry> m_vHistory = entry.History.CloneDeep();
                            entry.History = m_vHistory;
                            entry.CreateBackup(null);

                            entry.Strings.Remove(str.Key);

                            entry.Touch(true);

                            counter += 1;

                            break;
                        }
                    }
                }

                if (counter > 0)
                {
                    KeePass.Program.MainForm.UpdateUI(false, null, true, db.RootGroup, true, null, true);
                    MessageBox.Show(
                        String.Format("Successfully removed permissions from {0} entr{1}.", counter.ToString(), counter == 1 ? "y" : "ies"),
                        String.Format("Removed permissions from {0} entr{1}", counter.ToString(), counter == 1 ? "y" : "ies"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "The active database does not contain an entry with permissions.",
                        "No entry with permissions found!",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            else
            {
                MessageBox.Show("The active database is locked!\nPlease unlock the selected database or choose another one which is unlocked.", "Database locked!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void portNumber_ValueChanged(object sender, EventArgs e)
        {
            _restartRequired = (_config.ListenerPort != portNumber.Value);
        }

    }


}
