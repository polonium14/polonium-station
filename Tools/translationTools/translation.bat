@echo off

REM Taken from Corvax - https://github.com/space-syndicate/space-station-14/tree/master/Tools/ss14_ru

call pip install -r requirements.txt --no-warn-script-location
call python ./yamlextractor.py
call python ./keyfinder.py
call python ./clean_duplicates.py
call python ./clean_empty.py

PAUSE
