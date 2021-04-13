﻿using ProvisioningBuildTools.SelectInput;
using ProvisioningBuildTools.SelectOutput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ProvisioningBuildTools.Global;
using System.Configuration;
using System.Text.RegularExpressions;
using ProvisioningBuildTools.CLI;

namespace ProvisioningBuildTools.SelectForm
{
    public partial class frmSelectLoaclProjectForProvisioningTester : Form, ISelect<SelectProvisioningTesterInfoOutput>
    {
        private SelectProvisioningTesterInfoOutput m_SelectResult;
        public SelectProvisioningTesterInfoOutput SelectResult => m_SelectResult;

        public ILogNotify LogNotify { get; set; }
        public ICommandNotify CommandNotify { get; set; }
        public AbCLIExecInstance CLIInstance { get; set; }

        private string m_CommandLine;
        public string CommandLine => m_CommandLine;

        private SelectProvisioningTesterInfoInput input;

        private Action endInvoke;
        private Action startInvoke;
        private bool needDoubleConfirm = true;

        public frmSelectLoaclProjectForProvisioningTester(ILogNotify logNotify, ICommandNotify commandNotify)
        {
            InitializeComponent();
            LogNotify = logNotify;
            CommandNotify = commandNotify;

            input = new SelectProvisioningTesterInfoInput(logNotify);

            endInvoke = new Action(() => EnableRun(true));
            startInvoke = new Action(() => EnableRun(false));
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            GlobalValue.Root.SelectedProject = cmbProvisioningPorject.SelectedItem?.ToString();

            Project project = GlobalValue.Root.GetProject(GlobalValue.Root.SelectedProject);

            project = project ?? GlobalValue.Root.AddProject(GlobalValue.Root.SelectedProject);

            string packageName = cmbPackageName.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(packageName))
            {
                project.ProvisioningPackage = Path.Combine(txtPackageFolder.Text, packageName);
            }
            else
            {
                project.ProvisioningPackage = txtPackageFolder.Text;
            }

            project.SerialNumber = cmbSerialNumber.SelectedItem?.ToString();
            project.Slot = cmbSlot.SelectedItem?.ToString();
            project.TaskOpCodeList = string.Join(",", chkTaskList.CheckedItems.Cast<string>());

            string[] usuallyArray = string.IsNullOrWhiteSpace(project.UsuallyTaskOpCodeList) ? (new string[0]) : project.UsuallyTaskOpCodeList.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            project.UsuallyTaskOpCodeList = string.Join(",", chkTaskList.CheckedItems.Cast<string>().Concat(usuallyArray).Distinct(StringComparer.InvariantCulture));

            string args = null;

            ProvisioningTesterInfo provisioningTesterInfo = input.GetProvisioningTesterInfo(cmbProvisioningPorject.SelectedItem?.ToString());

            args = provisioningTesterInfo.ProvisioningPackageList[packageName].Value.GenerateProvisioningTesterArg(project.SerialNumber, project.Slot, rtbExec.Text.TrimEnd());

            if (File.Exists(Path.Combine(project.ProvisioningPackage, Command.ProvisioningTester)))
            {
                m_SelectResult = new SelectProvisioningTesterInfoOutput(
                    GlobalValue.Root.SelectedProject,
                    project.ProvisioningPackage,
                    project.SerialNumber,
                    project.Slot,
                    rtbExec.Text.TrimEnd(),
                    args,
                    provisioningTesterInfo.ProvisioningPackageList[packageName].Value.UseExternalProvisioningTester);

                string highlight = $"Please make sure{Environment.NewLine}{Environment.NewLine}ProvisioningPackage:{project.ProvisioningPackage}{Environment.NewLine}{Environment.NewLine}ProvisioningTesterArgs:{args}";

                if (!needDoubleConfirm || MessageBox.Show(highlight, "Double confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    if (CLIInstance != null)
                    {
                        CLIInstance.CommandLineFormatParas["project"] = cmbProvisioningPorject.SelectedItem?.ToString();
                        CLIInstance.CommandLineFormatParas["package"] = cmbPackageName.SelectedItem?.ToString();
                        CLIInstance.CommandLineFormatParas["serialnumber"] = cmbSerialNumber.SelectedItem?.ToString();
                        CLIInstance.CommandLineFormatParas["slot"] = cmbSlot.SelectedItem?.ToString();
                        CLIInstance.CommandLineFormatParas["task"] = string.Join(",", chkTaskList.CheckedItems.Cast<string>()).TrimEnd(',');
                        CLIInstance.CommandLineFormatParas["force"] = (!needDoubleConfirm).ToString().ToLower();
                        m_CommandLine = CLIInstance.GetCommandLine();
                    }

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            else
            {
                LogNotify.WriteLog($"{Command.ProvisioningTester} not found in package path {project.ProvisioningPackage}!!!", true);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void frmSelectLoaclProjectForProvisioningTester_Load(object sender, EventArgs e)
        {
            //cmbLocalBranches.IntegralHeight = false;           

            if (input.LocalBranches == null || input.LocalBranches.Length == 0)
            {
                this.btnOK.Enabled = false;
            }
            else
            {
                cmbProvisioningPorject.Items.AddRange(input.LocalBranches);
            }


            Utility.SetSelectedItem(cmbProvisioningPorject, GlobalValue.Root.SelectedProject);

            int maxAvailableSlot;

            string maxAvailableSlotStr = ConfigurationManager.AppSettings["maxAvailableSlot"];

            if (!int.TryParse(maxAvailableSlotStr, out maxAvailableSlot) || maxAvailableSlot <= 0)
            {
                maxAvailableSlot = 64;
            }

            for (int i = 0; i < maxAvailableSlot; i++)
            {
                cmbSlot.Items.Add(i.ToString());
            }

            Utility.SetSelectedItem(cmbSlot, GlobalValue.Root.GetProject(GlobalValue.Root.SelectedProject)?.Slot);

            chkTaskList.CheckOnClick = true;

            IssueBtnOk();
        }

        private void EnableRun(bool enable = true)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(EnableRun), enable);
            }
            else
            {
                this.Enabled = enable;
            }

        }

        private void chkTaskList_MouseDown(object sender, MouseEventArgs e)
        {
            chkTaskList.Tag = chkTaskList.SelectedItem;
        }

        private void chkTaskList_MouseUp(object sender, MouseEventArgs e)
        {
            object lastItem = chkTaskList.Tag;
            object currentItem = chkTaskList.SelectedItem;

            if (currentItem != lastItem && currentItem != null && lastItem != null)
            {

                int idx1 = chkTaskList.Items.IndexOf(lastItem);
                int idx2 = chkTaskList.Items.IndexOf(currentItem);

                bool selected1 = chkTaskList.CheckedIndices.Contains(idx1);
                bool selected2 = chkTaskList.CheckedIndices.Contains(idx2);

                chkTaskList.Items.RemoveAt(idx1);
                chkTaskList.Items.Insert(idx2, lastItem);
                chkTaskList.SetItemChecked(idx2, selected1);

                if (idx1 > idx2)
                {
                    chkTaskList.SetItemChecked(idx2 + 1, !selected2);
                }
                else
                {
                    chkTaskList.SetItemChecked(idx2 - 1, !selected2);
                }

                chkTaskList.SetSelected(idx2, true);
            }

            IssueExecConetent();
            IssueBtnOk();
        }

        private void IssueExecConetent()
        {
            this.rtbExec.ReadOnly = true;

            if (chkTaskList.CheckedItems.Count == 0)
            {
                this.rtbExec.Clear();
            }
            else
            {
                IEnumerable<string> checkedItems = chkTaskList.CheckedItems.Cast<string>();
                string cmd = null;

                if (!string.IsNullOrWhiteSpace(cmd = checkedItems.FirstOrDefault(item => item.ToUpper().TrimEnd() == "Cmd".ToUpper())))
                {
                    this.rtbExec.ReadOnly = false;

                    if (this.rtbExec.Text.ToUpper().TrimEnd().Contains("Cmd".ToUpper()))
                    {

                    }
                    else
                    {
                        this.rtbExec.Clear();
                        this.rtbExec.AppendText($"-{cmd.TrimEnd()} ");
                    }
                }
                else
                {
                    this.rtbExec.Clear();

                    this.rtbExec.AppendText($"-Task {string.Join(",", checkedItems)}");
                }
            }

        }

        private void IssueBtnOk()
        {
            if (string.IsNullOrWhiteSpace(cmbProvisioningPorject.SelectedItem?.ToString())
                //|| string.IsNullOrWhiteSpace(cmbPackageName.SelectedItem?.ToString())
                || cmbPackageName.SelectedItem == null
                || string.IsNullOrWhiteSpace(txtPackageFolder.Text.ToString())
                || string.IsNullOrWhiteSpace(cmbSerialNumber.SelectedItem?.ToString())
                || string.IsNullOrWhiteSpace(cmbSlot.SelectedItem?.ToString())
                 || string.IsNullOrWhiteSpace(rtbExec.Text.ToString()))
            {
                btnOK.Enabled = false;
            }
            else
            {
                Regex[] regices = new Regex[] { new Regex(@"-Task\s+\S+", RegexOptions.IgnoreCase), new Regex(@"-Cmd\s+\S+", RegexOptions.IgnoreCase) };

                btnOK.Enabled = false;
                foreach (var regex in regices)
                {
                    if (regex.IsMatch(rtbExec.Text))
                    {
                        btnOK.Enabled = true;
                        break;
                    }
                }
            }
        }


        private void cmbProvisioningPorject_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtPackageFolder.Text = string.Empty;
            cmbPackageName.Items.Clear();
            cmbSerialNumber.Items.Clear();
            chkTaskList.Items.Clear();

            if (cmbProvisioningPorject.SelectedItem != null)
            {
                ProvisioningTesterInfo provisioningTesterInfo = input.GetProvisioningTesterInfo(cmbProvisioningPorject.SelectedItem.ToString());

                txtPackageFolder.Text = provisioningTesterInfo.LocalProjectInfo.ProvisioningPackageFolder;
                cmbPackageName.Items.AddRange(provisioningTesterInfo.ProvisioningPackageList.Keys.ToArray());
                AdjustComboBoxDropDownListWidth(cmbPackageName);

                string provisioningPackage = GlobalValue.Root.GetProject(cmbProvisioningPorject.SelectedItem?.ToString())?.ProvisioningPackage;
                string packageName = null;

                if (!string.IsNullOrEmpty(provisioningPackage) && provisioningPackage.ToUpper().Contains(txtPackageFolder.Text.ToUpper()))
                {
                    packageName = provisioningPackage.ToUpper().Replace(txtPackageFolder.Text.ToUpper(), string.Empty).Trim(new char[] { '\\', '/' });
                }

                Utility.SetSelectedItem(cmbPackageName, packageName);

                Utility.SetSelectedItem(cmbSlot, GlobalValue.Root.GetProject(cmbProvisioningPorject.SelectedItem?.ToString())?.Slot, false);
            }
        }

        private void cmbProvisioningPackage_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbSerialNumber.Items.Clear();
            chkTaskList.Items.Clear();

            if (cmbPackageName.SelectedItem != null)
            {
                Project project = GlobalValue.Root.GetProject(cmbProvisioningPorject.SelectedItem?.ToString());

                ProvisioningTesterInfo provisioningTesterInfo = input.GetProvisioningTesterInfo(cmbProvisioningPorject.SelectedItem.ToString());

                cmbSerialNumber.Items.AddRange(provisioningTesterInfo.ProvisioningPackageList[cmbPackageName.SelectedItem.ToString()].Value.SerialNumberList.ToArray());
                Utility.SetSelectedItem(cmbSerialNumber, project?.SerialNumber);

                string[] items = provisioningTesterInfo.ProvisioningPackageList[cmbPackageName.SelectedItem.ToString()].Value.TaskList.ToArray();
                string[] lastCheckedItems = project?.TaskOpCodeList.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                string[] usuallyUsedItems = project?.UsuallyTaskOpCodeList.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                Utility.SetItems(chkTaskList, items, lastCheckedItems, usuallyUsedItems);

                IssueExecConetent();
                IssueBtnOk();
            }
        }

        private void AdjustComboBoxDropDownListWidth(object comboBox)
        {
            Graphics g = null;
            Font font = null;
            try
            {
                ComboBox senderComboBox = null;
                if (comboBox is ComboBox)
                    senderComboBox = (ComboBox)comboBox;
                else if (comboBox is ToolStripComboBox)
                    senderComboBox = ((ToolStripComboBox)comboBox).ComboBox;
                else
                    return;

                int width = senderComboBox.Width;
                g = senderComboBox.CreateGraphics();
                font = senderComboBox.Font;

                //checks if a scrollbar will be displayed.
                //If yes, then get its width to adjust the size of the drop down list.
                int vertScrollBarWidth =
                    (senderComboBox.Items.Count > senderComboBox.MaxDropDownItems)
                    ? SystemInformation.VerticalScrollBarWidth : 0;

                int newWidth;
                foreach (object s in senderComboBox.Items)  //Loop through list items and check size of each items.
                {
                    if (s != null)
                    {
                        newWidth = (int)g.MeasureString(s.ToString().Trim(), font).Width
                            + vertScrollBarWidth;
                        if (width < newWidth)
                            width = newWidth;   //set the width of the drop down list to the width of the largest item.
                    }
                }
                senderComboBox.DropDownWidth = width;
            }
            catch
            { }
            finally
            {
                if (g != null)
                    g.Dispose();
            }
        }

        private void txtPackageFolder_TextChanged(object sender, EventArgs e)
        {
            IssueBtnOk();
        }

        public void CLIExec()
        {
            this.Shown += frmSelectLoaclProject_Shown;
        }

        private void frmSelectLoaclProject_Shown(object sender, EventArgs e)
        {
            if (CLIInstance != null && CLIInstance.ParseSuccess && CLIInstance.FromCLI)
            {
                bool success = false;
                string project = CLIInstance.GetParameterValue("project", string.Empty);
                success = Utility.SetSelectedItem(cmbProvisioningPorject, project, false, true);

                if (success)
                {
                    bool requiredCompleted = true;
                    Dictionary<string, string> requiredParas = new Dictionary<string, string>();
                    requiredParas["package"] = CLIInstance.GetParameterValue("package", string.Empty);
                    requiredParas["serialnumber"] = CLIInstance.GetParameterValue("serialnumber", string.Empty);
                    requiredParas["slot"] = CLIInstance.GetParameterValue("slot", string.Empty);
                    requiredParas["task"] = CLIInstance.GetParameterValue("task", string.Empty);

                    bool succcess = false;

                    foreach (var requiredPara in requiredParas)
                    {

                        switch (requiredPara.Key)
                        {
                            case "package":
                                succcess = Utility.SetSelectedItem(cmbPackageName, requiredPara.Value, false, true);
                                break;
                            case "serialnumber":
                                succcess = Utility.SetSelectedItem(cmbSerialNumber, requiredPara.Value, false, true);
                                break;
                            case "slot":
                                succcess = Utility.SetSelectedItem(cmbSlot, requiredPara.Value, false, true);
                                break;
                            case "task":
                                succcess = Utility.SetSelectedItem(chkTaskList, requiredPara.Value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries), true, true);
                                break;
                            default:
                                break;
                        }

                        requiredCompleted = requiredCompleted && succcess;

                        if (!succcess)
                        {
                            string value = string.IsNullOrEmpty(requiredPara.Value) ? "NULL" : requiredPara.Value;
                            LogNotify.WriteLog($"CLI Error for {requiredPara.Key}:{value}, you need to manual select for {CLIInstance.CLIExecEnum}", true);
                        }
                    }

                    IssueExecConetent();
                    IssueBtnOk();

                    if (btnOK.Enabled && requiredCompleted)
                    {
                        needDoubleConfirm = !CLIInstance.GetParameterValueBool("force", false);

                        btnOK.PerformClick();
                    }
                }
                else
                {
                    project = string.IsNullOrEmpty(project) ? "NULL" : project;
                    LogNotify.WriteLog($"CLI Error for project:{project}, you need to manual select for {CLIInstance.CLIExecEnum}", true);
                }
            }
        }
    }
}