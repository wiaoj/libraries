using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Results; 
public static class ErrorOr {
    public static ErrorOr<Success> Success() {
        return ErrorOr<Success>.Success(Wiaoj.Results.Success.Default);
    }
    public static ErrorOr<TValue> Success<TValue>(TValue value) {
        return ErrorOr<TValue>.Success(value);
    }
} 