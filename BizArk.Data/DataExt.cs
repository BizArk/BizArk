﻿using BizArk.Core;
using BizArk.Core.Extensions.EnumerableExt;
using BizArk.Core.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace BizArk.Data.DataExt
{

	/// <summary>
	/// Extension methods for working with sql objects.
	/// </summary>
	public static class DataExt
	{

		#region DbParameter

		/// <summary>
		/// Adds a value to the end of the parameter collection.
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <param name="setNull">If true, sets the value to DBNull if it ConvertEx.IsEmpty is true.</param>
		/// <param name="dbType"></param>
		/// <param name="size"></param>
		/// <returns></returns>
		public static DbParameter AddParameter(this DbCommand cmd, string name, object value, bool setNull, DbType? dbType = null, int? size = null)
		{
			if (setNull && ConvertEx.IsEmpty(value))
				value = DBNull.Value;

			var param = cmd.CreateParameter();

			param.ParameterName = name;
			param.Value = value;

			if (dbType.HasValue) param.DbType = dbType.Value;
			if (size.HasValue) param.Size = size.Value;

			cmd.Parameters.Add(param);
			return param;
		}

		/// <summary>
		/// This will add an array of parameters to a DbCommand. This is used for an IN statement.
		/// Use the returned value for the IN part of your SQL call. (i.e. SELECT * FROM table WHERE field IN ({paramNameRoot}))
		/// </summary>
		/// <param name="cmd">The DbCommand object to add parameters to.</param>
		/// <param name="paramNameRoot">What the parameter should be named followed by a unique value for each value. This value surrounded by {} in the CommandText will be replaced.</param>
		/// <param name="values">The array of strings that need to be added as parameters.</param>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		public static IEnumerable<DbParameter> AddParameters<T>(this DbCommand cmd, string paramNameRoot, params T[] values)
		{
			return AddParameters<T>(cmd, paramNameRoot, values, 1);
		}

		/// <summary>
		/// This will add an array of parameters to a DbCommand. This is used for an IN statement.
		/// Use the returned value for the IN part of your SQL call. (i.e. SELECT * FROM table WHERE field IN ({paramNameRoot}))
		/// </summary>
		/// <param name="cmd">The DbCommand object to add parameters to.</param>
		/// <param name="values">The array of strings that need to be added as parameters.</param>
		/// <param name="paramNameRoot">What the parameter should be named followed by a unique value for each value. This value surrounded by {} in the CommandText will be replaced.</param>
		/// <param name="start">The beginning number to append to the end of paramNameRoot for each value.</param>
		/// <param name="separator">The string that separates the parameter names in the sql command.</param>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		public static IEnumerable<DbParameter> AddParameters<T>(this DbCommand cmd, string paramNameRoot, IEnumerable<T> values, int start = 1, string separator = ", ")
		{
			/* An array cannot be simply added as a parameter to a DbCommand so we need to loop through things and add it manually. 
			 * Each item in the array will end up being it's own DbParameter so the return value for this must be used as part of the
			 * IN statement in the CommandText.
			 */
			var newParams = new List<DbParameter>();
			var paramNames = new List<string>();
			var paramNbr = start;
			foreach (var value in values)
			{
				var paramName = $"@{paramNameRoot}{paramNbr}";
				paramNames.Add(paramName);
				newParams.Add(cmd.AddParameter(paramName, value, true));

				paramNbr++;
			}

			cmd.CommandText = cmd.CommandText.Replace("{" + paramNameRoot + "}", string.Join(separator, paramNames));

			return newParams;
		}

		/// <summary>
		/// Adds the objects properties as parameters.
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="parameters">Converts to a property bag and then adds the values to the command as parameters.</param>
		public static IEnumerable<DbParameter> AddParameters(this DbCommand cmd, object parameters)
		{
			var newParams = new List<DbParameter>();
			var propBag = ObjectUtil.ToPropertyBag(parameters);
			foreach (var prop in propBag)
			{
				var newParam = cmd.AddParameter(prop.Key, prop.Value, true);
				newParams.Add(newParam);
			}

			return newParams;
		}

		#endregion

		#region Debug SQL

		/// <summary>
		/// Converts the command into TSql that can be executed in Sql Server Management Studio.
		/// </summary>
		/// <param name="cmd"></param>
		/// <returns></returns>
		public static string DebugText(this DbCommand cmd)
		{
			// No parameters, no problem.
			if (cmd.Parameters.Count == 0)
				return DebugSqlCommandText(cmd);

			var sb = new StringBuilder();

			foreach (DbParameter param in cmd.Parameters)
			{
				var paramName = DebugSqlParamName(param);
				var type = DebugSqlType(param);
				var value = DebugSqlValue(param);
				sb.Append($"DECLARE {paramName} AS {type} = {value}\n");
			}

			sb.Append(DebugSqlCommandText(cmd));

			return sb.ToString();
		}

		private static string DebugSqlCommandText(DbCommand cmd)
		{
			switch (cmd.CommandType)
			{
				case CommandType.Text:
					return cmd.CommandText;
				case CommandType.StoredProcedure:
					var sb = new StringBuilder();

					sb.Append($"EXEC {cmd.CommandText} ");

					var first = true;
					foreach (DbParameter param in cmd.Parameters)
					{
						if (!first)
							sb.Append(", ");
						first = false;
						sb.Append($"{DebugSqlParamName(param)}");
					}

					return sb.ToString();
				default:
					throw new ArgumentException($"'{cmd.CommandType.ToString()}' is not a supported CommandType for DebugSql.", "cmd");
			}
		}

		private static object DebugSqlParamName(DbParameter param)
		{
			if (param.ParameterName.StartsWith("@"))
				return param.ParameterName;
			else
				return "@" + param.ParameterName;
		}

		private static object DebugSqlValue(DbParameter param)
		{
			var val = param.Value;
			if (val == null) return "NULL";
			if (val == DBNull.Value) return "NULL";

			switch (param.DbType)
			{
				case DbType.AnsiString:
				case DbType.AnsiStringFixedLength:
				case DbType.Time:
				case DbType.Xml:
				case DbType.Date:
				case DbType.DateTime:
				case DbType.DateTime2:
				case DbType.DateTimeOffset:
					return val; // $"'{val.ToString().Replace("'", "''")}'";

				case DbType.String:
				case DbType.StringFixedLength:
					return $"N'{val.ToString().Replace("'", "''")}'";

				case DbType.Binary:
					var bytes = val as IEnumerable<byte>;
					return $"0x{bytes.ToHex()}";

				case DbType.Boolean:
					return ConvertEx.ToBool(val) ? "1" : "0";

				default:
					return val.ToString();
			}
		}

		private static string DebugSqlType(DbParameter param)
		{
			switch (param.DbType)
			{
				case DbType.AnsiString:
				case DbType.AnsiStringFixedLength:
				case DbType.String:
				case DbType.StringFixedLength:
				case DbType.Binary:
					return $"{param.DbType.ToString().ToUpper()}({param.Size})";
				default:
					return param.DbType.ToString().ToUpper();
			}
		}

		#endregion
	}
}
