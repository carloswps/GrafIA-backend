using System;

namespace SmartIa.Application.Exceptions;

public class UnsupportedFileTypeException : Exception
{
    public UnsupportedFileTypeException(string message) : base(message) { }
}

public class InvalidDatasetException : Exception
{
    public InvalidDatasetException(string message) : base(message) { }
}