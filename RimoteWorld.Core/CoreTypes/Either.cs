using System;

namespace RimoteWorld.Core
{
    public class Either<TLeft, TRight>
    {
        private readonly object _obj;
        private readonly bool _isLeft;

        public Either(TLeft left)
        {
            _obj = left;
            _isLeft = true;
        }

        public Either(TRight right)
        {
            _obj = right;
            _isLeft = false;
        }

        public static implicit operator Either<TLeft, TRight>(TLeft left)
        {
            return new Either<TLeft, TRight>(left);
        }

        public static implicit operator Either<TLeft, TRight>(TRight right)
        {
            return new Either<TLeft, TRight>(right);
        }

        public bool IsLeft { get { return _isLeft; } }
        public bool IsRight { get { return !IsLeft; } }

        public TLeft Left
        {
            get { return (TLeft)_obj; }
        }

        public TRight Right
        {
            get { return (TRight)_obj; }
        }

        public void DoEither(Action<TLeft> ifLeft, Action<TRight> ifRight)
        {
            if (_isLeft)
            {
                ifLeft(Left);
            }
            else
            {
                ifRight(Right);
            }
        }

        public TResult DoEitherWithResult<TResult>(Func<TLeft, TResult> ifLeft, Func<TRight, TResult> ifRight)
        {
            if (_isLeft)
            {
                return ifLeft(Left);
            }
            else
            {
                return ifRight(Right);
            }
        }

        public TResult IfLeftOrDefault<TResult>(Func<TLeft, TResult> ifLeft)
        {
            return IfLeftWithResult(ifLeft, default(TResult));
        }

        public TResult IfLeftWithResult<TResult>(Func<TLeft, TResult> ifLeft, TResult defaultVal)
        {
            return DoEitherWithResult(ifLeft, (r) => defaultVal);
        }
        public void IfLeft(Action<TLeft> ifLeft)
        {
            DoEither(ifLeft, right => { });
        }

        public Either<TResult, TRight> ContinueWithLeftOrPropogate<TResult>(Func<TLeft, TResult> ifLeft)
        {
            return DoEitherWithResult<Either<TResult, TRight>>(left => ifLeft(left), right => right);
        }

        public TResult IfRightOrDefault<TResult>(Func<TRight, TResult> ifRight)
        {
            return IfRightWithResult(ifRight, default(TResult));
        }
        public TResult IfRightWithResult<TResult>(Func<TRight, TResult> ifRight, TResult defaultVal)
        {
            return DoEitherWithResult((l) => defaultVal, ifRight);
        }

        public void IfRight(Action<TRight> ifRight)
        {
            DoEitherWithResult(left => true, right =>
            {
                ifRight(right);
                return true;
            });
        }

        public TLeft LeftOrDefault(TLeft defaultValue)
        {
            return DoEitherWithResult((left) => left, (_) => defaultValue);
        }

        public TRight RightOrDefault(TRight defaultValue)
        {
            return DoEitherWithResult((_) => defaultValue, (right) => right);
        }
    }
}
