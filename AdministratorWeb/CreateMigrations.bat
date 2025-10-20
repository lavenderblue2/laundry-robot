@echo off
echo Creating new migration...
dotnet ef migrations add Migration_%random%
echo Migration created successfully!
pause