using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;

namespace UnityEngine.ResourceManagement
{
    class ChainOperation<TObject, TObjectDependency> : AsyncOperationBase<TObject>
    {
        AsyncOperationHandle<TObjectDependency> m_DepOp;
        AsyncOperationHandle<TObject> m_WrappedOp;
        Func<AsyncOperationHandle<TObjectDependency>, AsyncOperationHandle<TObject>> m_Callback;
        Action<AsyncOperationHandle<TObject>> m_CachedOnWrappedCompleted;
        bool m_ReleaseDependenciesOnFailure = true;

        public ChainOperation()
        {
            m_CachedOnWrappedCompleted = OnWrappedCompleted;
        }

        protected override string DebugName
        {
            get { return $"ChainOperation<{typeof(TObject).Name},{typeof(TObjectDependency).Name}> - {m_DepOp.DebugName}"; }
        }

        /// <inheritdoc />
        public override void GetDependencies(List<AsyncOperationHandle> deps)
        {
            if (m_DepOp.IsValid())
                deps.Add(m_DepOp);
        }

        public void Init(AsyncOperationHandle<TObjectDependency> dependentOp, Func<AsyncOperationHandle<TObjectDependency>, AsyncOperationHandle<TObject>> callback, bool releaseDependenciesOnFailure)
        {
            m_DepOp = dependentOp;
            m_DepOp.Acquire();
            m_Callback = callback;
            m_ReleaseDependenciesOnFailure = releaseDependenciesOnFailure;
        }

        ///<inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;

            if (!m_DepOp.IsDone)
                m_DepOp.WaitForCompletion();

            m_RM?.Update(Time.unscaledDeltaTime);

            if (!HasExecuted)
                InvokeExecute();

            if (!m_WrappedOp.IsValid())
                return m_WrappedOp.IsDone;
            m_WrappedOp.WaitForCompletion();
            return m_WrappedOp.IsDone;
        }

        protected override void Execute()
        {
            m_WrappedOp = m_Callback(m_DepOp);
            m_WrappedOp.Completed += m_CachedOnWrappedCompleted;
            m_Callback = null;
        }

        private void OnWrappedCompleted(AsyncOperationHandle<TObject> x)
        {
            OperationException ex = null;
            if (x.Status == AsyncOperationStatus.Failed)
                ex = new OperationException($"ChainOperation failed because dependent operation failed", x.OperationException);
            Complete(m_WrappedOp.Result, x.Status == AsyncOperationStatus.Succeeded, ex, m_ReleaseDependenciesOnFailure);
        }

        protected override void Destroy()
        {
            if (m_WrappedOp.IsValid())
                m_WrappedOp.Release();

            if (m_DepOp.IsValid())
                m_DepOp.Release();
        }

        internal override void ReleaseDependencies()
        {
            if (m_DepOp.IsValid())
                m_DepOp.Release();
        }
    }

    class ChainOperationTypelessDepedency<TObject> : AsyncOperationBase<TObject>
    {
        AsyncOperationHandle m_DepOp;
        AsyncOperationHandle<TObject> m_WrappedOp;
        Func<AsyncOperationHandle, AsyncOperationHandle<TObject>> m_Callback;
        Action<AsyncOperationHandle<TObject>> m_CachedOnWrappedCompleted;
        bool m_ReleaseDependenciesOnFailure = true;

        internal AsyncOperationHandle<TObject> WrappedOp => m_WrappedOp;

        public ChainOperationTypelessDepedency()
        {
            m_CachedOnWrappedCompleted = OnWrappedCompleted;
        }

        protected override string DebugName
        {
            get { return $"ChainOperation<{typeof(TObject).Name}> - {m_DepOp.DebugName}"; }
        }

        /// <inheritdoc />
        public override void GetDependencies(List<AsyncOperationHandle> deps)
        {
            if (m_DepOp.IsValid())
                deps.Add(m_DepOp);
        }

        public void Init(AsyncOperationHandle dependentOp, Func<AsyncOperationHandle, AsyncOperationHandle<TObject>> callback, bool releaseDependenciesOnFailure)
        {
            m_DepOp = dependentOp;
            m_DepOp.Acquire();
            m_Callback = callback;
            m_ReleaseDependenciesOnFailure = releaseDependenciesOnFailure;
        }

        ///<inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;

            if (!m_DepOp.IsDone)
                m_DepOp.WaitForCompletion();

            m_RM?.Update(Time.unscaledDeltaTime);

            if (!HasExecuted)
                InvokeExecute();

            if (!m_WrappedOp.IsValid())
                return m_WrappedOp.IsDone;
            Result = m_WrappedOp.WaitForCompletion();
            return true;
        }

        protected override void Execute()
        {
            m_WrappedOp = m_Callback(m_DepOp);
            m_WrappedOp.Completed += m_CachedOnWrappedCompleted;
            m_Callback = null;
        }

        private void OnWrappedCompleted(AsyncOperationHandle<TObject> x)
        {
            OperationException ex = null;
            if (x.Status == AsyncOperationStatus.Failed)
                ex = new OperationException($"ChainOperation failed because dependent operation failed", x.OperationException);
            Complete(m_WrappedOp.Result, x.Status == AsyncOperationStatus.Succeeded, ex, m_ReleaseDependenciesOnFailure);
        }

        protected override void Destroy()
        {
            if (m_WrappedOp.IsValid())
                m_WrappedOp.Release();

            if (m_DepOp.IsValid())
                m_DepOp.Release();
        }

        internal override void ReleaseDependencies()
        {
            if (m_DepOp.IsValid())
                m_DepOp.Release();
        }
    }
}
