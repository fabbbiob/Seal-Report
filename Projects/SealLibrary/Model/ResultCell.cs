﻿//
// Copyright (c) Seal Report, Eric Pfirsch (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. http://www.apache.org/licenses/LICENSE-2.0..
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Seal.Converter;
using Seal.Helpers;
using System.Web;
using System.Globalization;

namespace Seal.Model
{
    public class ResultCell
    {
        public object Value;
        public ReportElement Element;
        public bool IsTotal = false;
        public bool IsTitle = false;
        public bool IsTotalTotal = false;
        public bool IsSerie = false;

        //Final Values and CSS if set in the cell script
        public string FinalValue = "";
        public string FinalCssStyle = "";

        public string HTMLValue
        {
            get
            {
                if (!string.IsNullOrEmpty(FinalValue)) return FinalValue;
                return (Element != null && Element.HasHTMLTagsEl) ? DisplayValue : Helper.ToHtml(DisplayValue);
            }
        }

        public string CSVValue(bool useFormat, string separator)
        {
            string result = ExcelHelper.ToCsv(useFormat ? ValueNoHTML : RawDisplayValue, separator);
            if (Element != null && Element.HasHTMLTagsEl) result = Helper.RemoveHTMLTags(result);
            return result;
        }

        public string DisplayValue
        {
            get
            {
                try
                {
                    if (Value == null) return "";
                    if (IsTitle) return Element.Model.Report.TranslateElement(Element, Value.ToString());
                    if (Value is IFormattable) return ((IFormattable)Value).ToString(Element.FormatEl, Element.Model.Report.ExecutionView.CultureInfo);
                }
                catch { }
                return Value.ToString();
            }
        }

        public string ValueNoHTML
        {
            get
            {
                return (Element != null && Element.HasHTMLTagsEl) ? Helper.RemoveHTMLTags(DisplayValue) : DisplayValue;
            }
        }

        public string RawDisplayValue
        {
            get
            {
                if (Value == null) return "";
                if (IsTitle) return Value.ToString();
                return Value.ToString();
            }
        }

        public double? DoubleValue
        {
            get
            {
                if (Value == null || string.IsNullOrEmpty(Value.ToString())) return null;
                double result;
                if (double.TryParse(Value.ToString(), out result)) return result;
                return null;
            }
        }

        public string NavigationValue
        {
            get
            {
                string result = RawDisplayValue;
                if (Element.IsEnum)
                {
                    MetaEV enumValue = null;
                    if (Element.MetaColumn.Enum.Translate) enumValue = Element.MetaColumn.Enum.Values.FirstOrDefault(i => Element.Model.Report.EnumDisplayValue(Element.MetaColumn.Enum, i.Id) == result);
                    else enumValue = Element.MetaColumn.Enum.Values.FirstOrDefault(i => i.Val == result);
                    if (enumValue != null) result = enumValue.Id;
                }
                else if (Element.IsDateTime && Value is DateTime)
                {
                    result = ((DateTime)Value).ToOADate().ToString(CultureInfo.InvariantCulture);
                }
                return result;
            }
        }

        public List<ResultCell> SubReportValues = new List<ResultCell>();

        public DateTime? DateTimeValue
        {
            get
            {
                if (Value == null || string.IsNullOrEmpty(Value.ToString()) || !(Value is DateTime)) return null;
                return (DateTime)Value;
            }
        }

        public string Class
        {
            get
            {
                string result = IsTitle ? "empty_title" : "empty_value";
                if (Element != null)
                {
                    result = Element.PivotPosition.ToString().ToLower();
                    if (IsTitle) result += "_title";
                    else result += "_value";
                    if (IsTotal) result += "_total";
                }
                return result;
            }
        }

        public string CellCssStyle
        {
            get
            {
                if (!string.IsNullOrEmpty(FinalCssStyle)) return FinalCssStyle;

                string result = "";
                if (IsTitle) return result;
                else if (Element != null && !string.IsNullOrEmpty(Element.CellCss))
                {
                    //Handle multiple CSS definition
                    string[] css = Element.CellCss.Split('|');
                    if (css.Length == 1) result = Element.CellCss;
                    else if (css.Length >= 2)
                    {
                        result = ((Value != null && Value.ToString() == "") || (DoubleValue != null && DoubleValue.Value == 0)) ? css[1] : css[0];
                        if (css.Length == 3 && (DoubleValue != null && DoubleValue.Value != 0))
                        {
                            result = (DoubleValue.Value > 0) ? css[0] : css[2];
                        }
                    }
                }

                if (Element != null && !Element.IsEnum && string.IsNullOrEmpty(result))
                {
                    if (Element.IsNumeric || Element.IsDateTime) result = "text-align:right;";
                }
                return result;
            }
        }

        public static int CompareCells(ResultCell[] a, ResultCell[] b)
        {
            if (a.Length == 0 || a.Length != b.Length) return 0;
            ReportModel model = a[0].Element.Model;

            foreach (ReportElement element in model.Elements.OrderBy(i => i.FinalSortOrder))
            {
                ResultCell aCell = a.FirstOrDefault(i => i.Element == element);
                ResultCell bCell = b.FirstOrDefault(i => i.Element == element);
                if (aCell != null && bCell != null)
                {
                    int result = CompareCell(aCell, bCell);
                    if (result != 0) return (element.SortOrder.Contains(SortOrderConverter.kAscendantSortKeyword) ? 1 : -1) * result;
                }
            }
            return 0;
        }

        public static int CompareCell(ResultCell a, ResultCell b)
        {
            if (a.Value == DBNull.Value && b.Value == DBNull.Value) return 0;
            if (a.Value == DBNull.Value && b.Value != null) return -1;
            if (a.Value != null && b.Value == DBNull.Value) return 1;
            if (a.Element.IsEnum)
            {
                return a.Element.GetEnumSortValue(a.Value.ToString(), true).CompareTo(b.Element.GetEnumSortValue(b.Value.ToString(), true));
            }
            else if (a.Element.IsText)
            {
                return a.Value.ToString().CompareTo(b.Value.ToString());
            }
            else if (a.Element.IsDateTime)
            {
                if (a.DateTimeValue == b.DateTimeValue) return 0;
                return a.DateTimeValue > b.DateTimeValue ? 1 : -1;
            }
            else if (a.Element.IsNumeric)
            {
                if (a.DoubleValue == b.DoubleValue) return 0;
                return a.DoubleValue > b.DoubleValue ? 1 : -1;
            }
            return 0;
        }

        List<NavigationLink> _links = null;
        public List<NavigationLink> Links
        {
            get
            {
                //exe : execution guid of the source report
                //src : guid element source for drill
                //dst : guid element destination for drill
                //val : value of the restriction
                //res : guid element for a restriction
                //rpa : report path for sub-report
                //dis : display value for sub-report
 

                if (_links == null)
                {
                    _links = new List<NavigationLink>();
                    if (!IsTitle && !IsTotal && !IsTotalTotal && Element != null)
                    {
                        var report = Element.Source.Report;
                        if (report.IsDrillEnabled)
                        {
                            //Get Drill child links
                            var metaData = Element.Source.MetaData;
                            foreach (string childGUID in Element.MetaColumn.DrillChildren)
                            {
                                //Check that the element is not already in the model
                                if (Element.Model.Elements.Exists(i => i.MetaColumnGUID == childGUID && i.PivotPosition == Element.PivotPosition)) continue;

                                var child = metaData.GetColumnFromGUID(childGUID);
                                if (child != null)
                                {
                                    NavigationLink link = new NavigationLink();
                                    link.Href = string.Format("exe={0}&src={1}&dst={2}&val={3}", report.ExecutionGUID, Element.MetaColumnGUID, childGUID, HttpUtility.UrlEncode(NavigationValue));
                                    link.Text = HttpUtility.HtmlEncode(report.Translate("Drill >") + " " + report.Repository.RepositoryTranslate("Element", child.Category + '.' + child.DisplayName, child.DisplayName));

                                    _links.Add(link);
                                }
                            }

                            //Get drill parent link
                            //Element.MetaColumn.DillUpOnlyIfDD
                            foreach (MetaTable table in Element.Source.MetaData.Tables)
                            {
                                foreach(MetaColumn parentColumn in table.Columns.Where(i => i.DrillChildren.Contains(Element.MetaColumnGUID)))
                                {
                                    //Check that the element is not already in the model
                                    if (Element.Model.Elements.Exists(i => i.MetaColumnGUID == parentColumn.GUID && i.PivotPosition == Element.PivotPosition)) continue;

                                    if (Element.MetaColumn.DrillUpOnlyIfDD)
                                    {
                                        //check that the drill down occured
                                        if (!report.DrillParents.Contains(parentColumn.GUID)) continue;
                                    }

                                    NavigationLink link = new NavigationLink();
                                    link.Href = string.Format("exe={0}&src={1}&dst={2}", report.ExecutionGUID, Element.MetaColumnGUID, parentColumn.GUID);
                                    link.Text = HttpUtility.HtmlEncode(report.Translate("Drill <") + " " + report.Repository.RepositoryTranslate("Element", parentColumn.Category + '.' + parentColumn.DisplayName, parentColumn.DisplayName));
                                    _links.Add(link);
                                }
                            }
                        }

                        //Get sub reports links
                        if (Element.Source.Report.IsSubReportsEnabled)
                        {
                            foreach (var subreport in Element.MetaColumn.SubReports.Where(i => i.Restrictions.Count > 0))
                            {
                                string subReportRestrictions = "";
                                int index = 1;
                                foreach (var guid in subreport.Restrictions)
                                {
                                    var cellValue = SubReportValues.FirstOrDefault(i => i.Element.MetaColumnGUID == guid);
                                    if (cellValue != null)
                                    {
                                        subReportRestrictions += string.Format("&res{0}={1}&val{0}={2}", index, guid, HttpUtility.UrlEncode(cellValue.NavigationValue));
                                        index++;
                                    }
                                }
                                if (!string.IsNullOrEmpty(subReportRestrictions))
                                {
                                    NavigationLink link = new NavigationLink();
                                    link.Href = string.Format("rpa={0}", HttpUtility.UrlEncode(subreport.Path));
                                    if (subreport.Restrictions.Count > 1 || !subreport.Restrictions.Contains(Element.MetaColumn.GUID))
                                    {
                                        //Add the display value if necessary
                                        link.Href += string.Format("&dis={0}", HttpUtility.UrlEncode(DisplayValue));
                                    }
                                    link.Href += subReportRestrictions;
                                    link.Text = report.Repository.RepositoryTranslate("SubReport", Element.MetaColumn.Category + '.' + Element.MetaColumn.DisplayName, subreport.Name);
                                    _links.Add(link);
                                }
                            }
                        }
                    }
                }
                return _links;
            }
        }


        //Context to be used for cell script...
        public ReportModel ContextModel;
        public ResultPage ContextPage;
        public ResultTable ContextTable;
        public int ContextRow = -1;
        public int ContextCol = -1;

        public ResultCell[] ContextCurrentLine
        {
            get { return ContextTable != null && ContextRow != -1 ? ContextTable.Lines[ContextRow] : null; }
        }

        public bool ContextIsSummaryTable
        {
            get { return ContextPage == null; }
        }
    }
}
