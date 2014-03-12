﻿/* 
 * Boost Software License - Version 1.0 - August 17th, 2003
 * 
 * Copyright (c) 2013 Developed by reg <entry.reg@gmail.com>
 * 
 * Permission is hereby granted, free of charge, to any person or organization
 * obtaining a copy of the software and accompanying documentation covered by
 * this license (the "Software") to use, reproduce, display, distribute,
 * execute, and transmit the Software, and to prepare derivative works of the
 * Software, and to permit third-parties to whom the Software is furnished to
 * do so, all subject to the following:
 * 
 * The copyright notices in the Software and this entire statement, including
 * the above license grant, this restriction and the following disclaimer,
 * must be included in all copies of the Software, in whole or in part, and
 * all derivative works of the Software, unless such copies or derivative
 * works are solely in the form of machine-executable object code generated by
 * a source language processor.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
 * SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
 * FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE. 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using EnvDTE80;

namespace net.r_eg.vsSBE
{
    /// <summary>
    /// 
    /// TODO: logger for user
    /// </summary>
    class SBECommand
    {
        const string CMD_DEFAULT = "cmd";

        public class SBEContext
        {
            public string path;
            public string disk;

            public SBEContext(string path, string disk)
            {
                this.path = path;
                this.disk = disk;
            }
        }

        /// <summary>
        /// working directory for commands
        /// </summary>
        private SBEContext _context = null;

        /// <summary>
        /// DTE context
        /// </summary>
        private DTE2 _dte;

        /// <summary>
        /// type for recursive DTE commands
        /// </summary>
        private SBEQueueDTE.Type _queueType;
        private SBEQueueDTE.Rec _QueueRec
        {
            get { return SBEQueueDTE.queue[_queueType]; }
            set {
                if(!SBEQueueDTE.queue.ContainsKey(_queueType)) {
                    SBEQueueDTE.queue[_queueType] = value;
                }
            }
        }


        /// <summary>
        /// basic implementation
        /// </summary>
        /// <param name="evt">provided sbe-events</param>
        public bool basic(ISolutionEvent evt)
        {
            if(!evt.enabled){
                return false;
            }

            switch(evt.mode) {
                case TModeCommands.Operation: {
                    return hModeOperation(evt);
                }
                case TModeCommands.Interpreter: {
                    return hModeScript(evt);
                }
            }
            return hModeFile(evt);
        }

        public SBECommand(DTE2 dte, SBEQueueDTE.Type queueType)
        {
            _context    = new SBEContext(Config.WorkPath, _letDisk(Config.WorkPath));
            _dte        = dte;
            _queueType  = queueType;
            _QueueRec   = new SBEQueueDTE.Rec();
        }

        protected bool hModeFile(ISolutionEvent evt)
        {
            ProcessStartInfo psi = new ProcessStartInfo(CMD_DEFAULT);
            if(evt.processHide){
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }

            //TODO: [optional] capture message...

            string script = evt.command;

            if(evt.parseVariablesMSBuild) {
                script = (new MSBuildParser()).parseVariablesMSBuild(script);
            }

            string args = string.Format(
                "/C cd {0}{1} & {2}",
                _context.path, 
                (_context.disk != null) ? " & " + _context.disk + ":" : "",
                _treatNewlineAs(" & ", _modifySlash(script)));

            if(!evt.processHide && evt.processKeep){
                args += " & pause";
            }

            psi.Arguments       = args;
            Process process     = new Process();
            process.StartInfo   = psi;
            process.Start();

            if(evt.waitForExit){
                process.WaitForExit(); //TODO: !replace it on handling build
            }
            return true;
        }

        protected bool hModeOperation(ISolutionEvent evt)
        {
            if(_QueueRec.level == 0) {
                _QueueRec.cmd = evt.dteExec.cmd;
            }

            if(_QueueRec.cmd.Length < 1) {
                return true; //all pushed
            }

            ++_QueueRec.level;

            string[] newer = new string[_QueueRec.cmd.Length - 1];
            for(int i = 1; i < _QueueRec.cmd.Length; ++i) {
                newer[i - 1] = _QueueRec.cmd[i];
            }
            string current = _QueueRec.cmd[0];
            _QueueRec.cmd = newer;

            Exception terminated = null;
            try {
                // * error if command not available at current time
                // * recursive to Debug.Start, Debug.StartWithoutDebugging, etc.,
                _dte.ExecuteCommand(current);
            }
            catch(Exception e) {
                terminated = e;
            }

            if(_QueueRec.cmd.Length > 0) {
                //other.. like a File.Print, etc.
                hModeOperation(evt);
            }

            --_QueueRec.level;

            if(terminated != null) {
                throw new Exception(terminated.Message, terminated);
            }
            return true;
        }

        //TODO:
        protected bool hModeScript(ISolutionEvent evt)
        {
            if(evt.interpreter.Trim().Length < 1){
                return false;
            }
            //new ProcessStartInfo(evt.interpreter);

            string script = evt.command;

            if(evt.parseVariablesMSBuild) {
                script = (new MSBuildParser()).parseVariablesMSBuild(script);
            }

            script = _treatNewlineAs(evt.newline, script);

            if(evt.wrapper.Length > 0){
                script = evt.wrapper + script.Replace(evt.wrapper, "\\" + evt.wrapper) + evt.wrapper;
            }

            ProcessStartInfo psi = new ProcessStartInfo(CMD_DEFAULT);
            if(evt.processHide) {
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }

            string args = string.Format("/C cd {0}{1} & {2} {3}",
                                        _context.path,
                                        (_context.disk != null) ? " & " + _context.disk + ":" : "",
                                        evt.interpreter, //TODO: optional manually..
                                        script);

            if(!evt.processHide && evt.processKeep) {
                args += " & pause";
            }

            Debug.WriteLine(args);

            psi.Arguments       = args;
            Process process     = new Process();
            process.StartInfo   = psi;
            process.Start();

            //TODO: [optional] capture message...

            if(evt.waitForExit) {
                process.WaitForExit(); //TODO: !replace it on handling build
            }
            return true;
        }

        private string _modifySlash(string data)
        {
            return data.Replace("/", "\\");
        }

        private string _treatNewlineAs(string str, string data)
        {
            return data.Replace("\r", "").Replace("\n", str);
        }

        private static string _letDisk(string path)
        {
            if(path.Length < 1){
                return null;
            }
            return path.Substring(0, 1);
        }
    }

    /// <summary>
    /// Support recursive DTE commands
    /// e.g.:
    ///   exec - "Debug.Start"
    ///   exec - "Debug.Start"
    ///   exec - "File.Print"
    /// </summary>
    class SBEQueueDTE
    {
        public enum Type
        {
            PRE, POST, CANCEL, WARNINGS, ERRORS, OWP
        }

        public class Rec
        {
            public int level = 0;
            public string[] cmd;
        }
        public static Dictionary<Type, Rec> queue = new Dictionary<Type, Rec>();
    }
}
