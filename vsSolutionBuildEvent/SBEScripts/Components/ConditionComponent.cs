﻿/* 
 * Boost Software License - Version 1.0 - August 17th, 2003
 * 
 * Copyright (c) 2013-2014 Developed by reg [Denis Kuzmin] <entry.reg@gmail.com>
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
using System.Text.RegularExpressions;
using net.r_eg.vsSBE.Exceptions;
using net.r_eg.vsSBE.MSBuild;
using net.r_eg.vsSBE.SBEScripts.Exceptions;

namespace net.r_eg.vsSBE.SBEScripts.Components
{
    public class ConditionComponent: IComponent
    {
        /// <summary>
        /// Type of implementation
        /// </summary>
        public ComponentType Type
        {
            get { return ComponentType.Condition; }
        }

        /// <summary>
        /// Allows post-processing with MSBuild core
        /// </summary>
        public bool PostProcessingMSBuild
        {
            get { return postProcessingMSBuild; }
            set { postProcessingMSBuild = value; }
        }
        protected bool postProcessingMSBuild = false;

        /// <summary>
        /// For evaluating with SBE-Script
        /// </summary>
        protected ISBEScript script;

        /// <summary>
        /// For evaluating with MSBuild
        /// </summary>
        protected IMSBuild msbuild;

        /// <summary>
        /// Handling with current type
        /// </summary>
        /// <param name="data">mixed data</param>
        /// <returns>prepared and evaluated data</returns>
        public string parse(string data)
        {
            StringHandler hString = new StringHandler();

            Match m = Regex.Match(hString.protect(data), 
                                    String.Format(@"{0}            #1 - Condition
                                                    \s*
                                                    {1}            #2 - Body if true
                                                    (?:
                                                      \s*else\s*
                                                      {1}          #3 - Body if false (optional)
                                                    )?",
                                                    RPattern.RoundBracketsContent,
                                                    RPattern.CurlyBracketsContent
                                                 ), RegexOptions.IgnorePatternWhitespace);

            if(!m.Success) {
                throw new SyntaxIncorrectException("Failed ConditionComponent - '{0}'", data);
            }

            string condition    = hString.recovery(m.Groups[1].Value);
            string bodyIfTrue   = hString.recovery(m.Groups[2].Value);
            string bodyIfFalse  = (m.Groups[3].Success)? hString.recovery(m.Groups[3].Value) : String.Empty;

            return parse(condition, bodyIfTrue, bodyIfFalse);
        }

        /// <param name="env">Used environment</param>
        /// <param name="uvariable">Used instance of user-variables</param>
        public ConditionComponent(IEnvironment env, IUserVariable uvariable)
        {
            script  = new Script(env, uvariable);
            msbuild = new MSBuildParser(env, uvariable);
        }

        protected string parse(string condition, string ifTrue, string ifFalse)
        {
            Log.nlog.Debug("Condition-parse: started with - '{0}' :: '{1}' :: '{2}'", condition, ifTrue, ifFalse);

            Match m = Regex.Match(condition, @"^\s*
                                               (!)?         #1 - flag of inversion (optional)
                                               ([^=!~<>]+) #2 - left operand - boolean type if as a single
                                               (?:
                                                   (
                                                      ===
                                                    |
                                                      !==
                                                    |
                                                      ~=
                                                    |
                                                      ==
                                                    |
                                                      !=
                                                    |
                                                      >=
                                                    |
                                                      <=
                                                    |
                                                      >
                                                    |
                                                      <
                                                   )        #3 - operator      (optional with #4)
                                                   (.+)     #4 - right operand (optional with #3)
                                               )?$", 
                                               RegexOptions.IgnorePatternWhitespace);

            if(!m.Success) {
                throw new SyntaxIncorrectException("Failed ConditionComponent->parse - '{0}'", condition);
            }

            bool invert         = m.Groups[1].Success;
            string left         = spaces(m.Groups[2].Value);
            string coperator    = null;
            string right        = null;
            bool result         = false;

            if(m.Groups[3].Success) {
                coperator   = m.Groups[3].Value;
                right       = spaces(m.Groups[4].Value);
            }
            Log.nlog.Debug("Condition-parse: left: '{0}', right: '{1}', operator: '{2}', invert: {3}", left, right, coperator, invert);

            left = evaluate(left);

            if(right != null) {
                result = Values.cmp(left, evaluate(right), coperator);
            }
            else {
                result = Values.cmp((left == "1")? Values.VTRUE : (left == "0")? Values.VFALSE : left);
            }
            Log.nlog.Debug("Condition-parse: result is: '{0}'", result);
            return ((invert)? !result : result)? ifTrue : ifFalse;
        }

        /// <summary>
        /// Handling spaces
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected string spaces(string data)
        {
            Match m = Regex.Match(data.Trim(), // // ->" data  "<- or ->data<-
                                    String.Format(@"(?:
                                                      {0}   #1 - with space protection
                                                    |
                                                      (.*)  #2 - without
                                                    )?",
                                                    RPattern.DoubleQuotesContent
                                                 ), RegexOptions.IgnorePatternWhitespace);

            if(!m.Success) {
                throw new SyntaxIncorrectException("Failed ConditionComponent->spaces - '{0}'", data);
            }

            if(m.Groups[1].Success) {
                return StringHandler.normalize(m.Groups[1].Value);
            }
            return m.Groups[2].Value.Trim();
        }

        /// <param name="data">mixed</param>
        protected string evaluate(string data)
        {
            Log.nlog.Debug("Condition-evaluate: started with '{0}'", data);

            data = script.parse(data);
            Log.nlog.Debug("Condition-evaluate: evaluated data: '{0}' :: ISBEScript", data);

            if(PostProcessingMSBuild) {
                data = msbuild.parse(data);
                Log.nlog.Debug("Condition-evaluate: evaluated data: '{0}' :: IMSBuild", data);
            }
            return data;
        }
    }

    /// <summary>
    /// Result type for ConditionComponent
    /// </summary>
    public struct ConditionComponentResult
    {
        /// <summary>
        /// Left operand
        /// </summary>
        public string left;

        /// <summary>
        /// Right operand
        /// </summary>
        public string right;

        /// <summary>
        /// Operator of comparison
        /// </summary>
        public string coperator;

        /// <summary>
        /// Body if true
        /// </summary>
        public string ifTrue;

        /// <summary>
        /// Body if false
        /// </summary>
        public string ifFalse;
    }
}
