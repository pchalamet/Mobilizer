Mobilizer README

Mobilizer preprocesses .NET 1.0 and 1.1 binaries to insert code to
save and restore the call stack. This is useful for implementing
mobile agents, migratory applications, checkpointing jobs in cycle
stealing systems, etc.

A quick run-down of the sub-projects:

MobilizerRt: The library you bind to when writing agents.
Mobilizer:   Static preprocessing library.
Mobilize:    Command-line wrapper for Mobilizer.

There's a 'make.bat' file in the bin folder that builds the library, 
mobilizes a small sample application (factorial), and builds a simple host exe.

To actually run the little demo, open three console windows. In the 
first, run 'host.exe -listen 12345'; in the second, run 'host.exe 
-listen 12346'; in the third use host.exe to bootstrap with 'host.exe m_fact.exe MainClass Main'.

You can put host.exe on different machines and edit fact\fact.cs so it 
doesn't use the loopback adapter and the 'agent' will move between the 
machines. Note that you don't need to deploy m_fact.exe remotely, the 
host process also sends the code.

A few details:

1. Mobilizer uses reflection, so unfortunately the assembly loader must 
be able to find the program you're mobilizing. Basically, you have to 
have Mobilize.exe, the various DLLs and the thing you're mobilizing in 
the same folder during the preprocessing step.

2. When you do mobile code, remember the assembly loader is still in 
the background. So if you have e.g. fact.exe lying around in the same 
folder as host.exe, the assembly loader will grab that one, not the one 
you loaded with Reflection. Basically delete the un-mobilized program 
before you run to keep things sane. Look at how host.exe hooks the 
AppDomain::AssemblyResolve event to load the assembly it gets off the wire.

3. Because of limitations with Reflection the mobilizer doesn't 
reproduce custom attributes (although in the past it has been modified 
to reproduce specific attributes manually). This means that if you have 
Web service client-side proxies you should keep them in a separate 
assembly and not mobilize that assembly (and this unfortunately 
complicates mobile code scenarios because you must now transport the 
set of assemblies around.)

For other stuff see host\host.cs, particularly, creating a 'mobile context':

ContextCollection owner = new ContextCollection(); MobileContext ctx = 
new MobileContext(owner, null, method); ctx.Start(true);

Restoring a context (with binary serialization in this case):

ContextCollection owner =
	(ContextCollection)formatter.Deserialize(input);
client.Close();

foreach (MobileContext ctx in owner.Contexts) {
	ctx.Start(true);
}

And waiting to migrate:

object target = context.WaitForAll();
// if target != null, serialize context and send

There are some other things not demonstrated, e.g. the host can trigger 
migration by calling RequestUnwind on a context, just like the agent 
code can do (see fact\fact.cs); you can put [Atomic] on methods to 
prevent migration within those methods (including called methods); 
there are a few other methods on MobileContext, e.g. to manually lock 
and unlock migration for small critical sections.

Hope this helps. Give me an email if you have any questions: d.cooney@qut.edu.au