# Silver Screen
---
#### iOS - TV show tracker for iPad

Silver Screen is an iOS 8.0+ iPad app that keeps track of your favorite shows and lets you know when new episodes will air. Favorite shows can be manually added or synced with a TrakTV account for any show on your watchlist. You can browse the entire seasons catalog for a series, including specials.

##### Feature set includes:
+ TV show library manager (add, remove, sync)
+ Detailed series information including show metadata, seasons, episodes and cast
+ Notifications 5 minutes before an episode airs
+ Today widget extension that shows today's new episodes
+ TV show data is automatically refreshed so the content is always the latest

To sync your favorite shows in the cloud, create an account at http://trakt.tv and then sign in using the top left gear icon.

.
#### Mac Agent - Automated Episode Download Service

Optionally, you can install and run the Silver Screen Mac Agent which contains no dock icon and resides in your menu bar. When the agent is running, additional functionality will be present in the iOS app when bound to the same wifi network. These new features gracefully appear or disappear depending on the connection state. These new features give the user the ability to download any episode that is available via Torrent.

##### Feature set includes:
+ Kick off a download from the iOS app and monitor the progress
+ List of previous downloads and the status outcome
+ List of active downloads and ability to cancel
+ List of episodes that have aired in the past 3 weeks but are missing locally
+ Push notifications to iPad when a download completes

The iPad will automatically connect to the agent, there is no interaction needed from the user. The iPad will automatically disconnect when the app is closed or backgrounded.

##### Download process once initiated from iPad:
+ Agent searches for associated magnet torrent using S06E14 format or by broadcast date against kat.cr and thepiratebay.org
+ Results are culled and ordered based on highest seed
+ Agent will process the results starting at the top of the list
  + Agent will automate Transmission and monitor the progress
  + Agent will verify that the torrent file data contains a valid file (.mp4, .mkv, .avi, .wmv)
  + If a torrent is stagnant for too long (2 minutes and no bytes change), agent will move onto the next result
  + Agent will remove the magnet torrent from Transmission once the file download is complete
+ If the file is not already in MP4 format, Agent will convert the file to MP4 using embedded Handbrake CLI
+ Finally, the episode video is added to iTunes and tagged as a TV show with the corresponding metadata, ready to view using Apple TV
+ The torrent artifacts are added to the recycle bin and a push notification is sent to the iPad client