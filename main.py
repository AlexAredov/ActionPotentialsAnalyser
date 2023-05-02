from circle import *

file = "2.txt"
time, voltage = preprocess(file)
radius, x, y = circle(time, voltage)


plt.plot(time, voltage)
plt.gca().add_patch(plt.Circle((x, y), radius, color='lightblue', alpha=0.5))
plt.show()