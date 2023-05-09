from circle import *
import time 

start = time.time() ## точка отсчета времени

file = "2(2).txt"
time1, voltage = preprocess(file)

radius, x, y = circle(time1, voltage)
print(radius)

end = time.time() - start ## собственно время работы программы

print(end) ## вывод времени
plt.plot(time1, voltage)
plt.gca().add_patch(plt.Circle((x, y), radius, color='lightblue', alpha=0.5))
plt.show()