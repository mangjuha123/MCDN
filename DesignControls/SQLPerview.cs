﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using FastColoredTextBoxNS;


namespace MySqlClientDotNET.DesignControls {
    public partial class SQLPerview : Form {
        public SQLPerview() {
            InitializeComponent();
            tabExecuteSQL.ItemSize = new System.Drawing.Size(0, 1); // tab
            SuccessMsg = new FastColoredTextBoxNS.TextStyle(Brushes.Blue, null, FontStyle.Bold);
            ErrorMsg = new FastColoredTextBoxNS.TextStyle(Brushes.Red, null, FontStyle.Bold);
            scriptError = false;
            autoComlete = new FastColoredTextBoxNS.AutocompleteMenu(textSQL);
            ICollection<string> icol = MySqlConfig.ListKeyword;
            autoComlete.Items.SetAutocompleteItems(icol);
            autoComlete.AppearInterval = 100;
            isSuccessExecuted = false;
            stringBuilderLocalMsg = new StringBuilder(string.Empty);
            Language();
        }

        private void Language() {
            this.Text = LanguageApp.langFormSqlPreview["FTSQLPreview"];
            buttonApply.Text = LanguageApp.langFormSqlPreview["BTApply"];
            buttonCancle.Text = LanguageApp.langFormSqlPreview["BTCancel"];
            buttonBack.Text = LanguageApp.langFormSqlPreview["BTBack"];
            buttonOK.Text = LanguageApp.langFormSqlPreview["BTOk"];

            MSGAction1 = LanguageApp.langFormSqlPreview["MSGAction1"];
            MSGAction2 = LanguageApp.langFormSqlPreview["MSGAction2"];
            MSGNotingToExec = LanguageApp.langFormSqlPreview["MSGNothingToExec"];
            MSGSqlStat = LanguageApp.langFormSqlPreview["MSGSqlStat"];
            MSGErrorSql = LanguageApp.langFormSqlPreview["MSGErrorSql"];
            MSGSucc = LanguageApp.langFormSqlPreview["MSGSucc"];
            MSGFail = LanguageApp.langFormSqlPreview["MSGFail"];
        }

        #region diable close button
        private const int CP_NOCLOSE_BUTTON = 0x200;
        protected override CreateParams CreateParams {
            get {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        }
        #endregion

        public string MSGAction1 { get; set; }
        private string MSGAction2;
        private string MSGNotingToExec;
        private string MSGSqlStat;
        private string MSGErrorSql;
        private string MSGSucc;
        private string MSGFail;

        private MySqlScript scriptSQL;
        private StringBuilder stringBuilderLocalMsg;
        private bool scriptError;
        private MySqlTransaction trans;

        private FastColoredTextBoxNS.Style SuccessMsg;
        private FastColoredTextBoxNS.Style ErrorMsg;
        private FastColoredTextBoxNS.AutocompleteMenu autoComlete;

        public bool isSuccessExecuted { get; set; }
        public List<MySqlParameter> mySqlParams { get; set; }
        public void setSQLtext(string sql) {
            textSQL.Text = sql;
        }

        public bool ReadOnlySQLText {
            get {
                return textSQL.ReadOnly;
            }
            set {
                textSQL.ReadOnly = value;
            }
        }

        private void buttonApply_Click(object sender, EventArgs e) {
            textMessage.Text = "\n";
            scriptError = false;
            if (textSQL.Text.Equals("")) {
                Msg.Warrn(MSGNotingToExec);
                return;
            }
            try {
                var cmmd = AppConnection.Connection.CreateCommand();

                if (trans != null)
                    trans.Dispose();
                if (mySqlParams != null)
                    cmmd.Parameters.AddRange(mySqlParams.ToArray());

                trans = AppConnection.Connection.BeginTransaction();

                cmmd.Connection = AppConnection.Connection;
                cmmd.Transaction = trans;

                scriptSQL = new MySqlScript(AppConnection.Connection);
                scriptSQL.Command = cmmd;
                scriptSQL.Query = textSQL.Text;
                scriptSQL.StatementExecuted += new MySqlStatementExecutedEventHandler(scriptSQL_StatementExecuted);
                scriptSQL.Error += new MySqlScriptErrorEventHandler(scriptSQL_Error);
                scriptSQL.ScriptCompleted += new EventHandler(scriptSQL_ScriptCompleted);
                scriptSQL.ExecuteAsync();
            } catch (MySqlException ex) {
                Msg.Err("Error " + ex.Message);
            }
        }


        private void SetMesssageAction(string msg, string status) {
            stringBuilderLocalMsg.Append(MSGAction2).Append(" : ").Append(msg).Append(status).Append("\n");
        }

        private void SetMessageSqlStetment(string msg) {
            stringBuilderLocalMsg.Append(MSGSqlStat).Append(" : \n").Append(msg).Append("\n");
        }

        private void SetMessageError(string msg) {
            stringBuilderLocalMsg.Append(MSGErrorSql).Append(" : ").Append(msg).Append("\n");
        }

        void scriptSQL_StatementExecuted(object sender, MySqlScriptEventArgs args) {
            isSuccessExecuted = true;
            textExecuting.Text = args.StatementText;
            SetMesssageAction(MSGAction1, MSGSucc);
            SetMessageSqlStetment(args.StatementText);
            stringBuilderLocalMsg.Append("\n");
        }

        void scriptSQL_Error(object sender, MySqlScriptErrorEventArgs args) {
            SetMesssageAction(MSGAction1, MSGFail);
            SetMessageError(args.Exception.Message);
            stringBuilderLocalMsg.Append("\n");
            scriptError = true;
        }

        void scriptSQL_ScriptCompleted(object sender, EventArgs e) {
            if (scriptError) {
                ExecuteMessageEventArgs ev = new ExecuteMessageEventArgs();
                ev.Error = stringBuilderLocalMsg.ToString();
                onExecuted(ev);
                stringBuilderLocalMsg.Append("\n______________________________ERROR_______________________________");
                buttonBack.Visible = true;
            } else {
                ExecuteMessageEventArgs ev = new ExecuteMessageEventArgs();
                ev.Stetment = stringBuilderLocalMsg.ToString();
                onExecuted(ev);
                stringBuilderLocalMsg.Append("\n_____________________________SUCCESS______________________________");
                try {
                    if (trans != null)
                        trans.Commit();
                } catch (MySqlException ex) {
                    onErrorMessage("Commit Error", ex.Message);
                }
                buttonBack.Visible = false;
            }
            textMessage.Text = stringBuilderLocalMsg.ToString();
            stringBuilderLocalMsg.Clear();
            tabExecuteSQL.SelectedIndex = 2;
            textMessage.ScrollLeft();
            textMessage.Navigate(textMessage.Lines.Count - 1);
            textMessage.SelectionStart = textMessage.Text.Length;
        }

        private void onErrorMessage(string msgAction, string msgErr) {
            ExecuteMessageEventArgs exec = new ExecuteMessageEventArgs();
            exec.Action = MessageKeyword.BuildSqlActionMessage(msgErr);
            exec.Action = MessageKeyword.BuildErrorMessage(msgAction);
            if (ExcutedMessage != null)
                ExcutedMessage(this, exec);
        }

        private void buttonAbort_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void buttonBack_Click(object sender, EventArgs e) {
            tabExecuteSQL.SelectedIndex = 0;
            try {
                if (trans != null)
                    trans.Rollback();
                isSuccessExecuted = false;
            } catch (MySqlException ex) {
                onErrorMessage("Rollback Error", ex.Message);
            }
        }

        private void buttonOK_Click(object sender, EventArgs e) {
            if (scriptError) {
                if (trans != null) {
                    try {
                        trans.Commit();
                    } catch (MySqlException ex) {
                        onErrorMessage("Commit Error", ex.Message);
                    }
                }
            }
            this.Close();
        }

        private void textMessage_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e) {
            e.ChangedRange.SetStyle(SuccessMsg, "\r\n" + MSGAction2 + " \\:", RegexOptions.Singleline);
            e.ChangedRange.SetStyle(SuccessMsg, "\r\n" + MSGSqlStat + " \\:", RegexOptions.Singleline);
            e.ChangedRange.SetStyle(ErrorMsg, "\r\n" + MSGErrorSql + " \\:", RegexOptions.Singleline);
            e.ChangedRange.SetStyle(ErrorMsg, "\r\n______________________________ERROR_______________________________", RegexOptions.Singleline);
            e.ChangedRange.SetStyle(SuccessMsg, "\r\n_____________________________SUCCESS______________________________", RegexOptions.Singleline);
        }

        private void onExecuted(ExecuteMessageEventArgs e) {
            if (ExcutedMessage != null)
                ExcutedMessage(this, e);
        }

        public event EventHandler<ExecuteMessageEventArgs> ExcutedMessage;

        private void SQLPerview_Load(object sender, EventArgs e) {
            tabExecuteSQL.SelectedIndex = 0;
        }

        Style color = new TextStyle(Brushes.Blue, null, FontStyle.Regular);
        Style color_string = new TextStyle(Brushes.Red, null, FontStyle.Regular);
        Style color_name = new TextStyle(Brushes.Green, null, FontStyle.Regular);
        private void textSQL_TextChangedDelayed(object sender, FastColoredTextBoxNS.TextChangedEventArgs e) {
            e.ChangedRange.ClearStyle(color_string, color_name, color);
            e.ChangedRange.SetStyle(color_string, @"""""|@""""|''|@"".*?""|(?<!@)(?<range>"".*?[^\\]"")|'.*?[^\\]'");
            e.ChangedRange.SetStyle(color_name, @"``|(?<!@)`.*?[^\\]`", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(color, MySqlConfig.syntaxSQL, System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
