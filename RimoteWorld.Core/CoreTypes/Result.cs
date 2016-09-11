using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core
{
    public class Result<TResult, TError> where TError : Exception
    {
        private Either<TResult, TError> _either;

        public Result(TResult result)
        {
            _either = new Either<TResult, TError>(result);
        }

        public Result(TError ex)
        {
            _either = new Either<TResult, TError>(ex);
        }

        private Result(Either<TResult, TError> either)
        {
            _either = either;
        }

        public static implicit operator Result<TResult, TError>(TResult result)
        {
            return new Result<TResult, TError>(result);
        }

        public static implicit operator Result<TResult, TError>(TError err)
        {
            return new Result<TResult, TError>(err);
        }

        public TResult GetValueOrThrow()
        {
            return _either.DoEitherWithResult(left => left, right => { throw right; });
        }

        public bool IsError { get { return _either.IsRight; } }
        public TError Error { get { return _either.Right; } }

        public Result<TNewResult, TError> ContinueWithOrPropogate<TNewResult>(Func<TResult, TNewResult> continueWith)
        {
            return _either.DoEitherWithResult<Result<TNewResult, TError>>(left =>
            {
                try
                {
                    return continueWith(left);
                }
                catch (TError ex)
                {
                    return ex;
                }
            }, right => right);
        }

        public Result<TNewResult, TNewError> ContinueWithOrPropogateAsNewError<TNewResult, TNewError>(Func<TResult, TNewResult> continueWith, Func<TError, TNewError> errorConverter) where TNewError : Exception
        {
            return new Result<TNewResult, TNewError>(_either.DoEitherWithResult<Either<TNewResult, TNewError>>(left =>
            {
                try
                {
                    return continueWith(left);
                }
                catch (TError ex)
                {
                    return errorConverter(ex);
                }
            }, right => errorConverter(right)));
        }

        public Result<TResult> ToDefaultResult()
        {
            return _either.DoEitherWithResult<Result<TResult>>(left => left, right => right);
        }
    }

    public class Result<TResult> : Result<TResult, Exception>
    {
        public Result(TResult result) : base(result)
        {
            
        }

        public Result(Exception ex) : base(ex)
        {

        }

        public static implicit operator Result<TResult>(TResult result)
        {
            return new Result<TResult>(result);
        }

        public static implicit operator Result<TResult>(Exception err)
        {
            return new Result<TResult>(err);
        }
    }
}
