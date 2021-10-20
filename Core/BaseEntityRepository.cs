﻿using Core.Model;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public interface IBaseRepository : IDisposable
    {
        T GetAsNoTracking<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class;
        T GetAsNoTracking<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class;
        T Get<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class;
        T Get<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class;
        List<T> GetListAsNoTracking<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class;
        List<T> GetListAsNoTracking<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class;
        List<T> GetList<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class;
        List<T> GetList<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class;
        void RemoveRange<T>(IEnumerable<T> entities) where T : class;
        void Remove<T>(T entity) where T : class;
        void RemoveAndSave<T>(T entity) where T : class;
        void AddRange<T>(IEnumerable<T> entities) where T : BaseEntity;
        void AddRangeAndSave<T>(IEnumerable<T> entities) where T : BaseEntity;
        void Add<T>(T entity) where T : BaseEntity;
        void AddAndSave<T>(T entity) where T : BaseEntity;
        void Delete<T>(int id) where T : BaseEntity;
        void DeleteAndSave<T>(int id) where T : BaseEntity;
        void SaveContext();
        void RefreshContext();
    }


    public class BaseEntityRepository : IBaseRepository
    {
        protected BillingContext Context { get; set; }

        public BaseEntityRepository()
        {
            Context = new BillingContext();
        }

        public void SaveContext()
        {
            this.Context.ChangeTracker.DetectChanges();
            Context?.SaveChanges();
        }

        public void RefreshContext()
        {
            Context?.Dispose();
            Context = new BillingContext();
        }

        public List<T> ExecuteQuery<T>(string query)
        {
            var connection = Context.Database.GetDbConnection();
            return connection.Query<T>(query).ToList();
        }

        public T GetAsNoTracking<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class
        {
            var res = AddIncludes(QueryAsNoTracking<T>(), includes);
            if (res != null)
                return res.FirstOrDefault(predicate);
            return null;
        }

        public T GetAsNoTracking<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class
        {
            var res = AddIncludes(QueryAsNoTracking<T>(), includes);
            if (res != null)
                return res.FirstOrDefault(predicate);
            return null;
        }

        public List<T> GetListAsNoTracking<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class
        {
            var res = AddIncludes(QueryAsNoTracking<T>(), includes)
                .Where(predicate)
                .ToList();
            return res;
        }

        public List<T> GetListAsNoTracking<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class
        {
            var res = AddIncludes(QueryAsNoTracking<T>(), includes)
                .Where(predicate)
                .ToList();
            return res;
        }

        public virtual void AddAndSave<T>(T entity) where T : BaseEntity
        {
            Add(entity);
            SaveContext();
            Context.Entry(entity).Reload();
        }

        public virtual void Add<T>(T entity) where T : BaseEntity
        {
            if (entity.Id > 0)
                Context.Entry(entity).State = EntityState.Modified;
            else
                Context.Entry(entity).State = EntityState.Added;
        }

        public virtual void AddRange<T>(IEnumerable<T> entities) where T : BaseEntity
        {
            foreach (var entity in entities)
            {
                if (entity.Id > 0)
                    Context.Entry(entity).State = EntityState.Modified;
                else
                    Context.Entry(entity).State = EntityState.Added;
            }
        }

        public virtual void AddRangeAndSave<T>(IEnumerable<T> entities) where T : BaseEntity
        {
            foreach (var entity in entities)
            {
                if (entity.Id > 0)
                    Context.Entry(entity).State = EntityState.Modified;
                else
                    Context.Entry(entity).State = EntityState.Added;
            }
            SaveContext();
            foreach (var entity in entities)
            {
                Context.Entry(entity).Reload();
            }
        }

        public void Delete<T>(int id) where T : BaseEntity
        {
            var db = Get<T>(n => n.Id == id);
            if (db == null)
                throw new Exception("id not found");
            Remove(db);
        }

        public void DeleteAndSave<T>(int id) where T : BaseEntity
        {
            Delete<T>(id);
            SaveContext();
        }

        public virtual void Remove<T>(T entity) where T : class
        {
            Context.Set<T>().Remove(entity);
        }

        public virtual void RemoveRange<T>(IEnumerable<T> entities) where T : class
        {
            Context.Set<T>().RemoveRange(entities);
        }

        public virtual void RemoveAndSave<T>(T entity) where T : class
        {
            Remove(entity);
            SaveContext();
        }

        public virtual List<T> GetList<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class
        {
            var res = AddIncludes(Query<T>(), includes)
                .Where(predicate)
                .ToList();
            return res;
        }

        public virtual T Get<T>(Expression<Func<T, bool>> predicate, string[] includes) where T : class
        {
            var res = AddIncludes(Query<T>(), includes);
            if (res != null)
                return res.FirstOrDefault(predicate);
            return null;
        }

        public virtual T Get<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class
        {
            var res = AddIncludes(Query<T>(), includes);
            if (res != null)
                return res.FirstOrDefault(predicate);
            return null;
        }

        public virtual List<T> GetList<T>(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes) where T : class
        {
            var res = AddIncludes(Query<T>(), includes)
                .Where(predicate)
                .ToList();
            return res;
        }

        #region protected

        protected virtual IQueryable<T> Query<T>() where T : class
        {
            return Context.Set<T>();
        }

        protected virtual IQueryable<T> QueryAsNoTracking<T>() where T : class
        {
            return Context.Set<T>().AsNoTracking();
        }

        /// <summary>
        /// Add includes to query
        /// </summary>
        /// <param name="query">Query</param>
        /// <param name="includes">Navigation properties</param>
        /// <returns></returns>
        protected virtual IQueryable<T> AddIncludes<T>(IQueryable<T> query, Expression<Func<T, object>>[] includes) where T : class
        {
            if (includes != null)
            {
                foreach (var navigationProperty in includes)
                {
                    query = query.Include(navigationProperty);
                }
            }
            return query;
        }

        /// <summary>
        /// Add includes to query
        /// </summary>
        /// <param name="query">Query</param>
        /// <param name="includes">Navigation properties</param>
        /// <returns></returns>
        protected virtual IQueryable<T> AddIncludes<T>(IQueryable<T> query, string[] includes = null) where T : class
        {
            if (includes != null && includes.Length > 0)
            {
                foreach (string path in includes)
                {
                    query = query.Include(path);
                }
            }
            return query;
        }

        public void Dispose()
        {
            Context?.Dispose();
        }

        #endregion
    }
}
