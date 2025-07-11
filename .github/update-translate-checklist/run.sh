if [ github.event.action == 'closed' ]; then
  if [ github.event.pull_request.merged == true ]; then
    MODE=merge
  else MODE=nomerge
  fi
else MODE=open
fi

echo "Running update-translate-checklist with mode: $MODE"

dotnet run --project ./update-translate-checklist.csproj -- $MODE
