# Loupe Proxy for Web API #

This module extends a .NET Framework ASP.NET Web API 2 application to add a proxy for [Loupe Agent.](https://www.nuget.org/packages/Gibraltar.Agent/) traffic.
It will accept requests from a Loupe Agent as part of uploading Loupe data to a Loupe Server and proxy those
requests to the Loupe server that the Web API application is using.

The main use cases for this are:

* **Proxy Client Data from Secure Networks**:  Accept data from networked clients within a private network and
relay that information to a remote Loupe server, typically over the Internet.
* **Proxying Internet Client Data into Secure Networks**:  Accept data from remote Internet clients and safely
relay that information to a Loupe server on a private, secure network.

The main advantages of this proxy over simply gatewaying the Loupe Agent REST calls is that:

* **API Call Validation**: The proxy only relays REST calls that are valid for the Loupe Agent.
* **Agent Only**: API calls used by Loupe Desktop are not accepted, ensuring that no one can attempt
to read data via the proxy.
* **Response Obfuscation**: Any responses from the Loupe server that indicate a server-side issue or problem
are obfuscated to a common response and response messages are not relayed to ensure data doesn't leak from
the Loupe server out to the Agents.
* **Terminates All Calls**: All data uploaded is first received by the proxy, the length is validated and then it
is relayed on to the Loupe Server.  No request data is directly forwarded from the Agent to the Server.  Request
headers are redacted from the Agent so only Loupe-required headers are forwarded.

## Adding Loupe Proxy to your Application ##

To add the Loupe Proxy to your application we recommend referencing the [Loupe.Proxy.WebAPI package](https://www.nuget.org/packages/Loupe.Proxy.WebAPI/).
This will pull in the related dependencies.  It should be added to your ASP.NET WebAPI project.
Additionally, you will need to change your web.config to ensure that all modules are run for all
requests (so that the .xml and .zip requests that the Loupe Agent makes will be forwarded to ASP.NET)
like this:

```xml
<system.webServer>
    <modules runAllManagedModulesForAllRequests="true">
    </modules>
</system.webServer>
```

The Proxy will automatically start with your application and will trigger the Loupe Agent to start if it
hasn't already.  We recommend adding the Loupe Agent to your ASP.NET WebAPI application directly to
maximize the benefit of Loupe.  Follow this [Getting Started Guide](https://doc.onloupe.com/#GettingStarted_Introduction.html).

The Proxy depends on the Loupe Agent configuration for the ASP.NET application to know what server to
forward sessions to, so this must be configured and enabled for the Proxy to work.  For configuration
options, see [Server Configuration](https://doc.onloupe.com/DevelopersReference_ServerConfiguration.html).

You can use the [free Loupe Desktop viewer](https://onloupe.com/local-logging/free-net-log-viewer) to
view logs & analyze metrics for your application or use [Loupe Cloud-Hosted](https://onloupe.com/) to add centralized logging,
alerting, and error analysis to your application.

Once added, the proxy takes over requests sent to the path /loupe/hub in your application.  You can
verify that the proxy is running correctly by requesting the following:

```cmd
http://<path to the root of your application>/loupe/hub/configuration.xml
```

which should respond with the Hub Configuration in XML, similar to this:

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<HubConfigurationXml id="6fecb937-63ab-4ddd-8588-13f005828bce"
    xmlns="http://www.gibraltarsoftware.com/Gibraltar/Repository.xsd"
    protocolVersion="1.4" timeToLive="3600" status="available" redirectRequested="false" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <expirationDt Offset="0" DateTime="2030-12-30T23:59:59"/>
    <publicKey>BgIAAACkAABSU0ExAAQAAAEAAQDHCLluJFr3WTJzPgsd2vbkt+UxUHBvmnxcWGsjSOtCq/uvI8I8TVXdMwsC3a6bTXpYIr8s/cf5JZkidMzDdQzEoYxvHyQ2aZTra2bds8hdXRp/Pm3J1BYkP4v8po0UnsYyn5CYUdfQAzI/vPrcw03koOgK0Y3ifBEuIw2WhDIQjw==
    </publicKey>
</HubConfigurationXml>
```

If it doesn't, check the Loupe Log for your application for informational and warning messages from Loupe.Proxy.

## Configuring the Loupe Agent to use the Proxy ##

The Loupe Agent will need to be directed to the loupe subdirectory of your application to relay data
through the proxy.  The key configuration element is setting the applicationBaseDirectory to `loupe`.

### Example 1: Local Application Debugging ###

You will most likely initially test this all on your own development workstation.
Given that your application is running on `http://localhost:51252`, the appropriate
server configuration for the Loupe Agent would be:

```xml
<configuration>
  <gibraltar>
    <server enabled="true" server="localhost" port=51252 applicationBaseDirectory="loupe"
     autoSendSessions="true" sendAllApplications="true" />
  </gibraltar>
</configuration>
```

Of course, the port number will be different for your particular application as it is somewhat
randomly selected by Visual Studio.  

### Example 2: Deployed Application ###

Given an application deployed to `https://myapp.cloudapps.net/`, the appropriate
server configuration for the Loupe Agent would be:

```xml
<configuration>
  <gibraltar>
    <server enabled="true" useSsl="true" server="myapp.cloudapps.net" applicationBaseDirectory="loupe"
     autoSendSessions="true" sendAllApplications="true" />
  </gibraltar>
</configuration>
```

### Example 3: Application Deployed to a Subdirectory ###

If your application is not hosted in the site root but is instead in a subdirectory,
you will need to prepend the path to your application to the applicationBaseDirectory.
Given an application deployed to `https://myapp.cloudapps.net/my/sub/directory` the
appropriate server configuration for the Loupe Agent would be:

```xml
<configuration>
  <gibraltar>
    <server enabled="true" useSsl="true" server="myapp.cloudapps.net"
    applicationBaseDirectory="my/sub/directory/loupe"
     autoSendSessions="true" sendAllApplications="true" />
  </gibraltar>
</configuration>
```

## Verifying and Troubleshooting the Proxy ##

### Diagnosing Proxy Issues ###

To verify the proxy is running and able to process requests:

1. **Verify Proxy is Started**: When the proxy starts it will record log entries in the Loupe log
indicating that it was successful.  Look for messages in the category Loupe.Proxy near the start of the
web application.  A message with the caption "Loupe Proxy is Enabled" is written if the Proxy
could start and had what appeared to be a complete, valid configuration.  If there is a problem with the
configuration and the proxy can't start it will write out a warning message.
2. **Verify Proxy to Loupe Server Connectivity**:  Using a web browser, navigate to /loupe/hub/configuration.xml
relative to the base of your web application (as discussed above).  This call will be sent to the Loupe
Server and the response should be an XML document.  If you receive an HTML page that is not from your
application most likely the server configuration is not correct and is resolving to the wrong path on the Server.
If you receive an IIS error page make sure that you have set the system.webServer configuration to run
all modules for all requests as indicated in the documentation above.

### Diagnosing Agent Issues ###

If you suspect the Agent is not connecting to the Proxy, the diagnostic process is the same as
when diagnosing connections between the Agent and the Server:

1. **Verify Web Connectivity:** Using a web browser, connect to the hub configuration URL from
the computer where the Agent is running.  This must come up without any browser warnings for the
agent to connect.  A common problem is out-of-date certificates on the computer preventing SSL
connections from starting, thereby preventing the Agent from connecting.
2. **Enable Debug Mode**: The Loupe Agent has a [debug mode option (enableDebugMode)](https://doc.onloupe.com/DevelopersReference_PublisherConfiguration.html)
that when enabled will let the Agent include diagnostic information in its own log file.
Enable this option and re-run your application
then inspect the log looking for information about it attempting to connect to the server.  This
should provide details on the URL it is using (which will help you verify the server configuration
provided) and the error its receiving when attempting the connection.

## Impact of the Loupe Proxy on your Application ##

Our goal is that the Loupe Proxy has a negligible effect on the scalability and response time
of the host application.

The proxy is designed first for security then to be as low impact on the host application
as feasible.  It is fully asynchronous so it minimizes the impact on the ASP.NET request
thread pool available to your application.  It also minimizes memory usage by streaming
input data to disk then using that disk source to submit it up to the Loupe Server.  This
keeps it from allocating and freeing significant memory while processing requests.

The use of temporary files for transferring data means the Loupe Proxy will create and delete
a temporary file for many agent requests.

## What's In This Repository ##

This is the repository for the Loupe Proxy for WebAPI 2.
The following NuGet packages live here:

* Loupe.Proxy.WebAPI: The proxy for WebAPI 2.

Each of these packages maps to a single project in the repository. Other projects, primarily for unit testing, are not
included in the packages.

## How To Build These Projects ##

The various projects can all be built with Visual Studio 2017 by opening src\Loupe.Proxy.sln.

## Contributing ##

Feel free to branch this project and contribute a pull request to the development branch.
If your changes are incorporated into the master version they'll be published out to NuGet
for everyone to use!
